using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;
using KModkit;

public class ScramboozledEggainScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMSelectable FakeEggSel;
    public GameObject FakeEggObj;
    public TextMesh FakeEggText;
    public GameObject StatusLightParent;
    public GameObject FakeEggParent;
    public GameObject ScramboozledEggainParent;
    public GameObject[] FakeEggBkgds;
    public Material[] EggHighlightMats;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private bool _isEggMoving;

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        

        ScramboozledEggainParent.SetActive(false);
        FakeEggParent.SetActive(true);
        FakeEggSel.OnInteract += FakeEggPress;
        FakeEggSel.OnHighlight += delegate () { FakeEggObj.GetComponent<MeshRenderer>().material = EggHighlightMats[1]; };
        FakeEggSel.OnHighlightEnded += delegate () { FakeEggObj.GetComponent<MeshRenderer>().material = EggHighlightMats[0]; };
        FakeEggText.text = "";
    }

    private bool FakeEggPress()
    {
        if (!_isEggMoving)
        {
            var serialNumber = BombInfo.GetSerialNumber();
            var snDigit = serialNumber[5] - '0';
            var currentDigit = (int)BombInfo.GetTime() % 10;
            if (currentDigit != snDigit)
            {
                Module.HandleStrike();
                Debug.LogFormat("[Scramboozled Eggain #{0}] egg strikegged.", _moduleId);
            }
            else
            {
                _isEggMoving = true;
                Debug.LogFormat("Fake egg press");
                FakeEggText.text = "egg time?";
                StartCoroutine(FlyEgg());
            }
        }
        return false;
    }

    private IEnumerator FlyEgg()
    {
        var duration = 2f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            FakeEggObj.transform.localPosition = new Vector3(Mathf.Lerp(0f, 0.5f, elapsed / duration), Mathf.Lerp(0.0562f, 0.1f, elapsed / duration), Mathf.Lerp(-0.065f, -0.5f, elapsed / duration));
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
    }
}