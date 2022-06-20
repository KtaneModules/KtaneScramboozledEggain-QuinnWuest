using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;
using KModkit;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class ScramboozledEggainScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;

    // Fake Egg
    public KMSelectable FakeEggSel;
    public GameObject FakeEggObj;
    public TextMesh FakeEggText;
    public GameObject StatusLightParent;
    public GameObject FakeEggParent;
    public GameObject[] FakeEggBkgds;
    public Material[] EggHighlightMats;

    // Scramboozled Eggain
    public GameObject ScramboozledEggainParent;
    public KMSelectable[] EggSels;
    public GameObject[] EggObjs;
    public Material[] EggMats;
    public GameObject[] Eggzleglyphs;
    public Texture[] EggzleglyphTextures;
    public GameObject[] LEDObjs;
    public Material[] LEDMats;
    public TextMesh CongratsText;
    public GameObject[] PressLeds;
    public KMSelectable[] LedSels;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;
    private bool _isSolving;

    private bool _isBigEgg;
    private bool _isScramboozledEggain;
    private bool _isEggMoving;
    private List<int> _eggPresses = new List<int>();

    private static readonly string[] _wordList = new string[] { "BASTED", "BOILED", "BOXING", "CARTON", "DUMPTY", "FRENCH", "FRYPAN", "HUEVOS", "HUMPTY", "PASTEL", "ROYALE", "SARDOU", "SUNDAY", "TAJINE", "TRIFLE", "QUICHE", "WHITES", "ZYGOTE" };
    private string[] _selectedWords = new string[4];
    private int[] _eggColors = new int[6];
    private int[] _solution = new int[6];
    private int[][] _wordScrambles = new int[4][];
    private static readonly string _alphabet = "ABCDEFGHIJLMNOPQRSTUVWXYZ"; // K is intentionally missing

    private int _cycleIx;

    private string _serialNumber;
    private int _snDigit;
    private int _currentDigit;

    private bool _isCycling = true;
    private Coroutine _cycleDisplays;

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        FakeEggSel.OnHighlight += delegate () { FakeEggObj.GetComponent<MeshRenderer>().material = EggHighlightMats[1]; };
        FakeEggSel.OnHighlightEnded += delegate () { FakeEggObj.GetComponent<MeshRenderer>().material = EggHighlightMats[0]; };

        for (int i = 0; i < EggSels.Length; i++)
            EggSels[i].OnInteract += EggSelPress(i);
        for (int i = 0; i < LedSels.Length; i++)
            LedSels[i].OnInteract += LedSelPress(i);

        _isBigEgg = true;
        CongratsText.text = "";
        FakeEggText.text = "";
        ScramboozledEggainParent.SetActive(false);
        FakeEggParent.SetActive(true);
        FakeEggSel.OnInteract += FakeEggPress;
        _serialNumber = BombInfo.GetSerialNumber();
        _snDigit = _serialNumber[5] - '0';

        _eggColors = Enumerable.Range(0, 6).Select(i => i % 2).ToArray().Shuffle();
        _solution = Enumerable.Range(0, 6).OrderBy(i => _eggColors[i]).ToArray();
        for (int i = 0; i < 6; i++)
            EggObjs[i].GetComponent<MeshRenderer>().sharedMaterial = EggMats[_eggColors[i]];
        Debug.LogFormat("[Scramboozled Eggain #{0}] Initial order: {1}", _moduleId, _solution.Select(k => k + 1).Join(" "));

        _selectedWords = Enumerable.Range(0, _wordList.Length).ToArray().Shuffle().Select(i => _wordList[i]).Take(4).ToArray();
        _wordScrambles = Enumerable.Range(0, 4).Select(i => Enumerable.Range(0, 6).ToArray().Shuffle()).ToArray();
        Debug.LogFormat("[Scramboozled Eggain #{0}] Chosen words: {1}", _moduleId, _selectedWords.Join(", "));
        Debug.LogFormat("[Scramboozled Eggain #{0}] Chosen scrambles: {1}", _moduleId, _wordScrambles.Select(ws => ws.Select(j => j + 1).Join(" ")).Join(", "));

        for (int i = 0; i < 4; i++)
        {
            _solution = Enumerable.Range(0, 6).Select(egg => _solution[Array.IndexOf(_wordScrambles[i], egg)]).ToArray();
            Debug.LogFormat("[Scramboozled Eggain #{0}] After scramble #{1}, Order: {2}", _moduleId, i + 1, _solution.Select(j => j + 1).Join(" "));
        }
    }

    private KMSelectable.OnInteractHandler EggSelPress(int i)
    {
        return delegate ()
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, EggSels[i].transform);
            if (_moduleSolved)
                return false;
            _eggPresses.Add(i);
            SetPressLEDs();
            if (_eggPresses.Count == 6)
                CheckAnswer();
            return false;
        };
    }

    private KMSelectable.OnInteractHandler LedSelPress(int i)
    {
        return delegate ()
        {
            if (_isCycling)
            {
                if (i != 2)
                    return false;
                if (_cycleDisplays != null)
                    StopCoroutine(_cycleDisplays);
                _isCycling = false;
            }
            if (i == 2)
                return false;
            if (i == 1)
            {
                _cycleDisplays = StartCoroutine(CycleDisplays());
                _isCycling = true;
                return false;
            }
            if (i == 0)
                _cycleIx = (_cycleIx + 3) % 4;
            if (i == 3)
                _cycleIx = (_cycleIx + 1) % 4;
            SetScreens(_cycleIx);
            return false;
        };
    }

    private void SetPressLEDs()
    {
        for (int i = 0; i < 6; i++)
        {
            if (i < _eggPresses.Count)
                PressLeds[i].GetComponent<MeshRenderer>().sharedMaterial = LEDMats[1];
            else
                PressLeds[i].GetComponent<MeshRenderer>().sharedMaterial = LEDMats[0];
        }
    }

    private void CheckAnswer()
    {
        if (_eggPresses.SequenceEqual(_solution))
        {
            _isSolving = true;
            StartCoroutine(SolveAnimation());
            Debug.LogFormat("[Scramboozled Eggain #{0}] Pressed {1}. Module solved!", _moduleId, _eggPresses.Select(j => j + 1).Join(" "));
        }
        else
        {
            Module.HandleStrike();
            Debug.LogFormat("[Scramboozled Eggain #{0}] Pressed {1} instead of {2}. Strike.", _moduleId, _eggPresses.Select(j => j + 1).Join(" "), _solution.Select(j => j + 1).Join(" "));
        }
        _eggPresses = new List<int>();
        if (!_isSolving)
            SetPressLEDs();
    }

    private IEnumerator CycleDisplays()
    {
        while (!_moduleSolved && !_isSolving)
        {
            SetScreens(_cycleIx);
            yield return new WaitForSeconds(1f);
            if (!_isCycling)
                yield break;
            _cycleIx = (_cycleIx + 1) % 4;
        }
    }

    private void SetScreens(int ix)
    {
        for (int i = 0; i < 6; i++)
        {
            Eggzleglyphs[_wordScrambles[ix][i] * 2].GetComponent<MeshRenderer>().material.mainTexture = EggzleglyphTextures[_alphabet.IndexOf(_selectedWords[ix][i]) % 5];
            Eggzleglyphs[_wordScrambles[ix][i] * 2 + 1].GetComponent<MeshRenderer>().material.mainTexture = EggzleglyphTextures[_alphabet.IndexOf(_selectedWords[ix][i]) / 5];
        }
        for (int i = 0; i < 4; i++)
        {
            if (i == ix)
                LEDObjs[i].GetComponent<MeshRenderer>().material = LEDMats[1];
            else
                LEDObjs[i].GetComponent<MeshRenderer>().material = LEDMats[0];
        }
    }

    private bool FakeEggPress()
    {
        if (!_isEggMoving)
        {
            _currentDigit = (int)BombInfo.GetTime() % 10;
            if (_currentDigit != _snDigit)
            {
                Module.HandleStrike();
                Debug.LogFormat("[Scramboozled Eggain #{0}] Did not press the big egg on the correct digit. Strike.", _moduleId);
            }
            else
            {
                _isBigEgg = false;
                _isEggMoving = true;
                Debug.LogFormat("[Scramboozled Eggain #{0}] Successfully pressed the big egg. Transitioning to Scramboozled Eggain.", _moduleId);
                FakeEggText.text = "egg time?";
                StartCoroutine(FlyEgg());
            }
        }
        return false;
    }

    private IEnumerator FlyEgg()
    {
        _cycleDisplays = StartCoroutine(CycleDisplays());
        var duration = 2f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            FakeEggObj.transform.localPosition = new Vector3(Mathf.Lerp(0f, 0.5f, elapsed / duration), Mathf.Lerp(0.075f, 0.1f, elapsed / duration), Mathf.Lerp(-0.085f, -0.5f, elapsed / duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        FakeEggObj.SetActive(false);
        yield return new WaitForSeconds(2f);
        duration = 1f;
        elapsed = 0f;
        while (elapsed < duration)
        {
            FakeEggBkgds[0].transform.localEulerAngles = new Vector3(0f, 180f, Easing.InQuad(elapsed, 0f, -180f, duration));
            FakeEggBkgds[1].transform.localEulerAngles = new Vector3(Easing.InQuad(elapsed, 180f, 0f, duration), 90f, 0f);
            StatusLightParent.transform.localEulerAngles = new Vector3(0f, 0f, Easing.InQuad(elapsed, 0f, 180f, duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        FakeEggBkgds[0].SetActive(false);
        FakeEggText.text = "";
        ScramboozledEggainParent.SetActive(true);
        elapsed = 0f;
        while (elapsed < duration)
        {
            StatusLightParent.transform.localEulerAngles = new Vector3(0f, 0f, Easing.OutQuad(elapsed, 180f, 360f, duration));
            ScramboozledEggainParent.transform.localEulerAngles = new Vector3(0f, 0f, Easing.OutQuad(elapsed, 180f, 360f, duration));
            FakeEggBkgds[1].transform.localEulerAngles = new Vector3(Easing.OutQuad(elapsed, 0f, -180f, duration), 90f, 0f);
            yield return null;
            elapsed += Time.deltaTime;
        }
        StatusLightParent.transform.localEulerAngles = new Vector3(0f, 0f, 0f);
        ScramboozledEggainParent.transform.localEulerAngles = new Vector3(0f, 0f, 0f);
        FakeEggBkgds[1].transform.localEulerAngles = new Vector3(180f, 90f, 0f);
        Audio.PlaySoundAtTransform("Klaxon", transform);
        _isScramboozledEggain = true;
    }

    private IEnumerator SolveAnimation()
    {
        Audio.PlaySoundAtTransform("SolveSound", transform);
        for (int i = 0; i < Eggzleglyphs.Length; i++)
            Eggzleglyphs[i].SetActive(false);
        for (int i = 0; i < 4; i++)
            LEDObjs[i].GetComponent<MeshRenderer>().material = LEDMats[0];
        yield return new WaitForSeconds(2.2f);
        for (int i = 0; i < 4; i++)
            LEDObjs[i].GetComponent<MeshRenderer>().material = LEDMats[1];
        CongratsText.text = "EGGCELENT!";
        Module.HandlePass();
        _moduleSolved = true;
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} egg 4 [Press the egg when the last digit of the timer is a 4] | !{0} press 123456 [Presses buttons 1 2 3 4 5 6 in order. Must have 6 numbers in the command.] | !{0} led l [Press the left LED. LEDs are L, ML, MR, R.]";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        var m = Regex.Match(command, @"^\s*press\s+([1-6]{6})\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            if (!_isScramboozledEggain)
            {
                yield return "sendtochaterror You have not pressed the big egg yet!";
                yield break;
            }
            if (m.Groups[1].Value.Distinct().Count() != 6)
            {
                yield return "sendtochaterror Expected six different button presses.";
                yield break;
            }
            yield return null;
            _eggPresses = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                EggSels[m.Groups[1].Value[i] - '1'].OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            yield break;
        }
        m = Regex.Match(command, @"^\s*egg\s+([0-9])\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            if (!_isBigEgg)
            {
                yield return "sendtochaterror You have already pressed the big egg!";
                yield break;
            }
            yield return null;
            while ((int)BombInfo.GetTime() % 10 != m.Groups[1].Value[0] - '0')
                yield return null;
            FakeEggSel.OnInteract();
            yield break;
        }
        if (!command.StartsWith("led "))
            yield break;
        command = command.Substring(4);
        var ixs = new[] { "l", "ml", "mr", "r" };
        int ix = Array.IndexOf(ixs, command);
        if (ix == -1)
            yield break;
        if (!_isScramboozledEggain)
        {
            yield return "sendtochaterror You have not pressed the big egg yet!";
            yield break;
        }
        yield return null;
        LedSels[ix].OnInteract();
        yield break;
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        if (_isBigEgg)
        {
            while ((int)BombInfo.GetTime() % 10 != _snDigit)
                yield return true;
            FakeEggSel.OnInteract();
        }
        while (!_isScramboozledEggain)
            yield return true;
        _eggPresses = new List<int>();
        for (int i = 0; i < 6; i++)
        {
            EggSels[_solution[i]].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        while (!_moduleSolved)
            yield return true;
    }
}