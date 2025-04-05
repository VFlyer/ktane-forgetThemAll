using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using rnd = UnityEngine.Random;
using System.Text.RegularExpressions;

public class forgetThemAllScript : MonoBehaviour
{
    public KMBombInfo bomb;
    public KMAudio MAudio;
    public KMBombModule modSelf;
    public KMBossModule bossHandler;

    static string[] ignoredModules;

    public TextMesh stageNo, queryStagesCount;
    public GameObject queryStageObject;

    public HandleWireCut[] wireHandlers;

    public TextMesh[] colorblindTexts;
    public Renderer[] lightsRenderer;

    public KMSelectable[] wireInt;
    public Material[] wireColors;

    public Material[] lightColors;

    //Logging
    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    int[] colors = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

    bool readyToSolve = false;

    int stageCount;
    int currentStage = 0;
    int lastCalcStage = -1;
    List<int> wiresCut = new List<int>();
    StageInfo[] stages;
    int keyStage;
    List<int> cutOrder = new List<int>();


    float delayer = 5f;
    public List<string> solvedModuleNames = new List<string>();
    public List<string> queuedSolvedNames = new List<string>();
    int startTime;

    bool colorblindDetected = false;
    public KMColorblindMode colorblindMode;
    bool hasDetonated = false, isHardcoreBossModule, requestMercyAnim;

    IEnumerator flashIndicator;
    int wiresIncorrectlyCut = 0;//, strikeCountUponActivation;

    private ForgetThemAllSettings ftaSettings;

    void Awake()
    {
        moduleId = moduleIdCounter++;
        modSelf.OnActivate += Activate;

        for (int x = 0; x < wireInt.Length; x++)
        {
            int y = x;
            wireInt[x].OnInteract += delegate () { CutWire(y); return false; };
        }
        // Inefficient coding, more efficient coding is provided above.
        /*		wireInt[0].OnInteract += delegate () { CutWire(0); return false; };
				wireInt[1].OnInteract += delegate () { CutWire(1); return false; };
				wireInt[2].OnInteract += delegate () { CutWire(2); return false; };
				wireInt[3].OnInteract += delegate () { CutWire(3); return false; };
				wireInt[4].OnInteract += delegate () { CutWire(4); return false; };
				wireInt[5].OnInteract += delegate () { CutWire(5); return false; };
				wireInt[6].OnInteract += delegate () { CutWire(6); return false; };
				wireInt[7].OnInteract += delegate () { CutWire(7); return false; };
				wireInt[8].OnInteract += delegate () { CutWire(8); return false; };
				wireInt[9].OnInteract += delegate () { CutWire(9); return false; };
				wireInt[10].OnInteract += delegate () { CutWire(10); return false; };
				wireInt[11].OnInteract += delegate () { CutWire(11); return false; };
				wireInt[12].OnInteract += delegate () { CutWire(12); return false; };*/
        //stageNo.text = "---";
        stageNo.text = "";

        bomb.OnBombExploded += delegate ()
        {
            hasDetonated = true;
            if (!readyToSolve)
                CalcFinalSolution();
        };

        try
        {
            colorblindDetected = colorblindMode.ColorblindModeActive;
        }
        catch (Exception error)
        {
            colorblindDetected = false;
            Debug.LogErrorFormat("<Forget Them All #{0}> An exception occured on Forget Them All involving Colorblind Mode. This is not a severe issue. It might just be someone forgetting to assign something again.", moduleId);
            Debug.LogException(error);
        }
        for (int i = 0; i < 13; i++)
        {
            colorblindTexts[i].text = "";
        }
    }
    void Start()
    {
        try
        {
            ModConfig<ForgetThemAllSettings> localSettings = new ModConfig<ForgetThemAllSettings>("ForgetThemAllSettings");
            ftaSettings = localSettings.Settings;

            localSettings.Settings = ftaSettings;

            isHardcoreBossModule = ftaSettings.hardcoreBossModule;
        }
        catch (Exception error)
        {
            Debug.LogErrorFormat("[Forget Them All #{0}] An exception occured on Forget Them All involving its settings. Default settings will be used instead.", moduleId);
            Debug.LogException(error);
            isHardcoreBossModule = false;
        }

        if (ignoredModules == null)
            ignoredModules = bossHandler.GetIgnoredModules("Forget Them All", new string[]{// Default Ignore List
				"Forget Them All",
                "Alchemy",
                "Forget Everything",
                "Forget Infinity",
                "Forget Me Not",
                "Forget This",
                "Purgatory",
                "Souvenir",
                "Cookie Jars",
                "Divided Squares",
                "Hogwarts",
                "The Swan",
                "Turn the Keys",
                "The Time Keeper",
                "Timing is Everything",
                "Turn the Key"
            });
        
        RandomizeColors();
        Debug.LogFormat("[Forget Them All #{0}] Stage recovery is {1} for this module.", moduleId, isHardcoreBossModule ? "disabled" : "enabled");
    }
    bool canStart = false;
    void Activate()
    {
        startTime = (int)(bomb.GetTime() / 60);
        if (CheckAutoSolve())
        {
            Debug.LogFormat("[Forget Them All #{0}] There are 0 modules not ignored on this bomb. Autosolving...", moduleId);
            StartCoroutine(HandleSolving());
            return;
        }
        CreateStages();
        canStart = true;
    }
    IEnumerator HandleSolving()
    {
        moduleSolved = true;
        while (stageNo.text.Length > 0)
        {
            string curText = stageNo.text;
            stageNo.text = curText.Substring(0, curText.Length - 1);
            yield return new WaitForSeconds(0.1f);
        }
        modSelf.HandlePass();
        stageNo.text = "";
        queryStagesCount.text = "";
        for (int i = 0; i < 13; i++)
        {
            lightsRenderer[i].material = lightColors[13];
            colorblindTexts[i].text = "";
        }
        yield return null;
    }
    int frameAnim = 30;
    bool requestForceSolve = false;
    void Update()
    {
        float maxDelay = 3f;

        if (moduleSolved || !canStart)
            return;

        //if (delayer < 5f)
        //    delayer += Time.deltaTime;

        List<string> curSolvedModules = bomb.GetSolvedModuleNames().Where(a => !ignoredModules.Contains(a)).ToList();

        foreach (string solvedModName in solvedModuleNames)
            curSolvedModules.Remove(solvedModName);
        foreach (string queuedSolvedName in queuedSolvedNames)
            curSolvedModules.Remove(queuedSolvedName);
        if (curSolvedModules.Count > 0)
            queuedSolvedNames.AddRange(curSolvedModules);
        if (queuedSolvedNames.Any() || requestMercyAnim)
        {
            frameAnim = Math.Max(0, frameAnim - 1);
        }
        else
        {
            frameAnim = Math.Min(30, frameAnim + 1);
        }
        if (!moduleSolved && !readyToSolve)
            queryStagesCount.text = string.Format("+{0}", Math.Min(queuedSolvedNames.Count, 99).ToString("00"));
        queryStageObject.transform.localPosition = new Vector3(-.006f - frameAnim * .001f, queryStageObject.transform.localPosition.y, queryStageObject.transform.localPosition.z);

        if (delayer >= maxDelay && (lastCalcStage == -1 || queuedSolvedNames.Count > 0) && lastCalcStage < currentStage && !readyToSolve)
        {
            lastCalcStage++;
            if (lastCalcStage - 1 >= 0 && lastCalcStage - 1 < stages.Length)
            {
                stages[lastCalcStage - 1].SetModuleName(queuedSolvedNames[0]);
                solvedModuleNames.Add(queuedSolvedNames[0]);
                queuedSolvedNames.RemoveAt(0);
            }
            DisplayNextStage();
            delayer = 0f;
        }
        else if ((lastCalcStage == -1 || queuedSolvedNames.Count > 0) && lastCalcStage < currentStage && !readyToSolve)
        {
            delayer = requestForceSolve ? maxDelay : Mathf.Min(maxDelay, delayer += Time.deltaTime);
        }
    }

    void CutWire(int wire)
    {
        MAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.WireSnip, wireInt[wire].transform);

        wiresCut.Add(wire);

        if (!readyToSolve && !moduleSolved)
        {
            Debug.LogFormat("[Forget Them All #{0}] Strike! [{1}] wire cut before module is ready to be solved.", moduleId, GetColorName(colors[wire]));
            modSelf.HandleStrike();
            //hasStruck = true;
            return;
        }
        lightsRenderer[wire].material = lightColors[13];
        colorblindTexts[wire].text = "";
        if (moduleSolved)
            return;
        int index = cutOrder.IndexOf(colors[wire]);
        if (index != -1)
            cutOrder.RemoveAt(index);
        requestMercyAnim = false;
        queryStagesCount.text = "+00";
        if (flashIndicator != null)
        {
            StopCoroutine(flashIndicator);
            ShowFinalStage();
        }
        if (index == 0)
        {
            Debug.LogFormat("[Forget Them All #{0}] Successfully cut {1} wire.", moduleId, GetColorName(colors[wire]));
            if (cutOrder.Count() == 0)
            {
                Debug.LogFormat("[Forget Them All #{0}] All necessary wires have been cut in order successfully. Module disarmed.", moduleId);
                StartCoroutine(HandleSolving());
            }
            else
            {
                Debug.LogFormat("[Forget Them All #{0}] The remaining wires to cut in order are: {1}.", moduleId, ListToString(cutOrder));
            }
            wiresIncorrectlyCut = 0;

        }
        else if (cutOrder.Any())
        {
            Debug.LogFormat("[Forget Them All #{0}] Strike! Incorrectly cut {1} wire when it was expecting {2} wire.", moduleId, GetColorName(colors[wire]), GetColorName(cutOrder[0]));
            modSelf.HandleStrike();
            //hasStruck = true;
            if (!isHardcoreBossModule)
            {
                wiresIncorrectlyCut = 1;
                if (wiresIncorrectlyCut == 1)
                {
                    flashIndicator = HandleMercyAnim();
                }
                else
                {
                    Debug.LogFormat("[Forget Them All #{0}] You cut two or more wires incorrectly. The next wire that must be cut will now blink.", moduleId);
                    flashIndicator = FlashCorrectWire();
                }
                StartCoroutine(flashIndicator);
            }
            Debug.LogFormat("[Forget Them All #{0}] The remaining wires to cut in order are: {1}.", moduleId, ListToString(cutOrder));
        }
        else if (!moduleSolved)
        {
            Debug.LogFormat("[Forget Them All #{0}] There are no wires to cut in order, module disarmed.", moduleId);
            StartCoroutine(HandleSolving());
        }
    }
    IEnumerator HandleMercyAnim()
    {
        requestMercyAnim = true;
        for (int i = 0; i < 13; i++)
        {
            lightsRenderer[i].material = lightColors[13];
            colorblindTexts[i].text = "";
        }
        queryStagesCount.text = "+00";//strikeCountUponActivation.ToString("00") + "X";
        yield return new WaitForSeconds(3f);

        for (int x = 0; x < stages.Length; x++)
        {
            StageInfo s = stages[x];
            for (int i = 0; i < s.LED.Count(); i++)
            {
                int idx = Array.IndexOf(colors, i);
                lightsRenderer[idx].material = s.LED[i] ^ s.LEDToggledByKeyword[i] ? lightColors[i] : lightColors[13];
                colorblindTexts[idx].text = colorblindDetected && s.LED[i] ^ s.LEDToggledByKeyword[i] ? GetColorAbbrev(colors[idx]) : "";
                colorblindTexts[idx].color = "WLYI".Contains(colorblindTexts[idx].text) ? Color.black : Color.white;
            }
            stageNo.text = ((x + 1) % 1000).ToString("000");
            var solveActivatedModule = s.moduleName.ToUpper().Where(a => "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".Contains(a)).Join("");
            queryStagesCount.text = string.IsNullOrEmpty(solveActivatedModule) ? "" : solveActivatedModule.Substring(0, Mathf.Min(3, solveActivatedModule.Length));
            yield return new WaitForSeconds(2f);
        }
        requestMercyAnim = false;
        queryStagesCount.text = "+00";
        ShowFinalStage();
        yield return null;
    }
    IEnumerator FlashCorrectWire()
    {
        int lastCutGoalCount = cutOrder.Count;
        while (cutOrder.Count == lastCutGoalCount)
        {
            int i = cutOrder.FirstOrDefault();
            int idx = Array.IndexOf(colors, i);
            lightsRenderer[idx].material = lightColors[i];
            colorblindTexts[idx].text = colorblindDetected ? GetColorAbbrev(colors[idx]) : "";
            colorblindTexts[idx].color = "WLYI".Contains(colorblindTexts[idx].text) ? Color.black : Color.white;
            yield return new WaitForSeconds(0.5f);
            lightsRenderer[idx].material = lightColors[13];
            colorblindTexts[idx].text = "";
            yield return new WaitForSeconds(0.5f);
        }
        yield return null;
    }

    string GetColorName(int color)
    {
        switch (color)
        {
            case 0:
                return "Yellow";
            case 1:
                return "Grey";
            case 2:
                return "Blue";
            case 3:
                return "Green";
            case 4:
                return "Orange";
            case 5:
                return "Red";
            case 6:
                return "Lime";
            case 7:
                return "Cyan";
            case 8:
                return "Brown";
            case 9:
                return "White";
            case 10:
                return "Purple";
            case 11:
                return "Magenta";
            case 12:
                return "Pink";
            default:
                return "";
        }
    }

    string GetColorAbbrev(int color)
    {
        switch (color)
        {
            case 0:
                return "Y";
            case 1:
                return "A";
            case 2:
                return "B";
            case 3:
                return "G";
            case 4:
                return "O";
            case 5:
                return "R";
            case 6:
                return "L";
            case 7:
                return "C";
            case 8:
                return "N";
            case 9:
                return "W";
            case 10:
                return "P";
            case 11:
                return "M";
            case 12:
                return "I";
            default:
                return "?";
        }
    }
    void RandomizeColors()
    {
        colors.Shuffle();

        for (int i = 0; i < wireHandlers.Count(); i++)
        {
            wireHandlers[i].UpdateWireColor(wireColors[colors[i]]);
        }
    }

    bool CheckAutoSolve()
    {
        stageCount = bomb.GetSolvableModuleNames().Where(x => !ignoredModules.Contains(x)).Count();
        return stageCount == 0;
    }

    void CreateStages()
    {
        stages = new StageInfo[stageCount];
        for (int i = 0; i < stages.Count(); i++)
        {
            stages[i] = new StageInfo(i + 1, moduleId);
        }
    }

    void DisplayNextStage()
    {
        currentStage++;
        if (currentStage > stageCount)
        {
            if (readyToSolve)
                return;

            readyToSolve = true;

            ShowFinalStage();

            if (wiresCut.Count() >= 13)
            {
                Debug.LogFormat("[Forget Them All #{0}] All wires were cut upon the module being ready to calculate. Disarming...", moduleId);
                StartCoroutine(HandleSolving());
                return;
            }
            queryStagesCount.text = "+00";
            CalcFinalSolution();
            CalcWireOrder();
            return;
        }

        StageInfo s = stages[currentStage - 1];

        Debug.LogFormat("[Forget Them All #{0}] --------------------------- Stage {1} ---------------------------", moduleId, currentStage);
        Debug.LogFormat("[Forget Them All #{0}] Stage {1} LED: {2}", moduleId, currentStage, s.GetOnLED());

        for (int i = 0; i < s.LED.Count(); i++)
        {
            int idx = Array.IndexOf(colors, i);
            lightsRenderer[idx].material = s.LED[i] ? lightColors[i] : lightColors[13];
            colorblindTexts[idx].text = colorblindDetected && s.LED[i] ? GetColorAbbrev(colors[idx]) : "";
            colorblindTexts[idx].color = "WLYI".Contains(colorblindTexts[idx].text) ? Color.black : Color.white;
        }

        stageNo.text = (currentStage % 1000).ToString("000");
    }
    void DisplayCurrentStage(int stageNum)
    {
        if (stageNum <= 0 || stageNum - 1 >= stages.Length) return;
        StageInfo s = stages[stageNum - 1];

        for (int i = 0; i < s.LED.Count(); i++)
        {
            int idx = Array.IndexOf(colors, i);
            lightsRenderer[idx].material = s.LED[i] ? lightColors[i] : lightColors[13];
            colorblindTexts[idx].text = colorblindDetected && s.LED[i] ? GetColorAbbrev(colors[idx]) : "";
            colorblindTexts[idx].color = "WLYI".Contains(colorblindTexts[idx].text) ? Color.black : Color.white;
        }

        stageNo.text = (currentStage % 1000).ToString("000");
    }
    void ShowFinalStage()
    {
        stageNo.text = "---";

        for (int i = 0; i < 13; i++)
        {
            int idx = Array.IndexOf(colors, i);
            lightsRenderer[idx].material = wiresCut.Contains(idx) ? lightColors[13] : lightColors[i];
            colorblindTexts[idx].text = colorblindDetected && !wiresCut.Contains(idx) ? GetColorAbbrev(colors[idx]) : "";
            colorblindTexts[idx].color = "WLYI".Contains(colorblindTexts[idx].text) ? Color.black : Color.white;
        }
    }

    void CalcFinalSolution()
    {
        var requireStageToSolve = true;
        if (!readyToSolve && stages != null && solvedModuleNames.Count + 1 >= stages.Length)
        {
            Debug.LogFormat("[Forget Them All #{0}] There is exactly 1 unsolved non-ignored module left upon detonation. Calculating as if the module is ready to solve.", moduleId);
            List<string> remainingUnsolved = bomb.GetSolvableModuleNames().Where(a => !ignoredModules.Contains(a)).ToList();
            foreach (string aModName in solvedModuleNames)
                remainingUnsolved.Remove(aModName);
            foreach (string aModName in queuedSolvedNames)
                remainingUnsolved.Remove(aModName);
            if (remainingUnsolved.Any())
                stages[lastCalcStage].SetModuleName(remainingUnsolved.First());
            else if (queuedSolvedNames.Any())
                stages[lastCalcStage].SetModuleName(queuedSolvedNames.First());
        }
        else
            requireStageToSolve = false;
        Debug.LogFormat(hasDetonated ? "[Forget Them All #{0}] ----------------------- Upon Detonation -----------------------"
            : "[Forget Them All #{0}] --------------------------- Solving ---------------------------", moduleId, currentStage);
        if (stages == null || !stages.Any())
        {
            Debug.LogFormat("[Forget Them All #{0}] Bomb detonated before stages could be generated.", moduleId);
            return;
        }
        int[] totalLED = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };


        foreach (StageInfo si in stages)
        {
            if (si.moduleName != null)
                totalLED = totalLED.Select((x, index) => x + (si.LED[index] ? 1 : 0)).ToArray();
        }

        Debug.LogFormat("[Forget Them All #{0}] ", moduleId, currentStage);


        int aaBattery = bomb.GetBatteryCount(Battery.AA);
        int portPlates = bomb.GetPortPlateCount();
        int dupPorts = bomb.CountDuplicatePorts();
        int moduleCount = bomb.GetModuleNames().Count();
        int strikeCount = bomb.GetStrikes();
        int snTotal = bomb.GetSerialNumberNumbers().Sum();
        int snLetters = bomb.GetSerialNumberLetters().Count();
        int portTypes = bomb.CountUniquePorts();
        int onInd = bomb.GetOnIndicators().Count();
        int offInd = bomb.GetOffIndicators().Count();
        int dBattery = bomb.GetBatteryCount(Battery.D);
        // Calculate the final stage by the multiplier amount specified for each color and how many times that color has occured overall (after broken LED modifier).
        int yellow = totalLED[0] * aaBattery;
        int grey = totalLED[1] * portPlates;
        int blue = totalLED[2] * startTime;
        int green = totalLED[3] * dupPorts;
        int orange = totalLED[4] * moduleCount;
        int red = totalLED[5] * strikeCount;
        int lime = totalLED[6] * snTotal;
        int cyan = totalLED[7] * snLetters;
        int brown = totalLED[8] * portTypes;
        int white = totalLED[9] * onInd;
        int purple = totalLED[10] * totalLED[10];
        int magenta = totalLED[11] * offInd;
        int pink = totalLED[12] * dBattery;

        //strikeCountUponActivation = strikeCount;

        Debug.LogFormat("[Forget Them All #{0}] Yellow ocurrences: {1}. Multiplier: {2}. LED value: {3}.", moduleId, totalLED[0], aaBattery, yellow);
        Debug.LogFormat("[Forget Them All #{0}] Grey ocurrences: {1}. Multiplier: {2}. LED value: {3}.", moduleId, totalLED[1], portPlates, grey);
        Debug.LogFormat("[Forget Them All #{0}] Blue ocurrences: {1}. Multiplier: {2}. LED value: {3}.", moduleId, totalLED[2], startTime, blue);
        Debug.LogFormat("[Forget Them All #{0}] Green ocurrences: {1}. Multiplier: {2}. LED value: {3}.", moduleId, totalLED[3], dupPorts, green);
        Debug.LogFormat("[Forget Them All #{0}] Orange ocurrences: {1}. Multiplier: {2}. LED value: {3}.", moduleId, totalLED[4], moduleCount, orange);
        Debug.LogFormat("[Forget Them All #{0}] Red ocurrences: {1}. Multiplier: {2}. LED value: {3}.", moduleId, totalLED[5], strikeCount, red);
        Debug.LogFormat("[Forget Them All #{0}] Lime ocurrences: {1}. Multiplier: {2}. LED value: {3}.", moduleId, totalLED[6], snTotal, lime);
        Debug.LogFormat("[Forget Them All #{0}] Cyan ocurrences: {1}. Multiplier: {2}. LED value: {3}.", moduleId, totalLED[7], snLetters, cyan);
        Debug.LogFormat("[Forget Them All #{0}] Brown ocurrences: {1}. Multiplier: {2}. LED value: {3}.", moduleId, totalLED[8], portTypes, brown);
        Debug.LogFormat("[Forget Them All #{0}] White ocurrences: {1}. Multiplier: {2}. LED value: {3}.", moduleId, totalLED[9], onInd, white);
        Debug.LogFormat("[Forget Them All #{0}] Purple ocurrences: {1}. Multiplier: {2}. LED value: {3}.", moduleId, totalLED[10], totalLED[10], purple);
        Debug.LogFormat("[Forget Them All #{0}] Magenta ocurrences: {1}. Multiplier: {2}. LED value: {3}.", moduleId, totalLED[11], offInd, magenta);
        Debug.LogFormat("[Forget Them All #{0}] Pink ocurrences: {1}. Multiplier: {2}. LED value: {3}.", moduleId, totalLED[12], dBattery, pink);

        int value = yellow + grey + blue + green + orange + red + lime + cyan + brown + white + purple + magenta + pink;

        keyStage = value % stageCount;
        if (keyStage == 0)
            keyStage = stageCount;

        Debug.LogFormat(requireStageToSolve || readyToSolve ? "[Forget Them All #{0}] Final value: {1}. Key stage: {2} - {3}." : "[Forget Them All #{0}] Value up to this point: {1}", moduleId, value, keyStage, stages[keyStage - 1].moduleName);
    }

    void CalcWireOrder()
    {
        List<int> wiresToCut = new List<int>();

        for (int i = 0; i < 13; i++)
        {
            if (!wiresCut.Contains(i))
                wiresToCut.Add(colors[i]);
        }

        for (int i = 0; i < stages[keyStage - 1].moduleName.Length; i++)
        {
            int charColor = GetCharColor(stages[keyStage - 1].moduleName[i]);
            int index = wiresToCut.FindIndex(x => x == charColor);
            if (index >= 0)
            {
                cutOrder.Add(charColor);
                wiresToCut.RemoveAt(index);
            }
        }
        if (cutOrder.Any())
            Debug.LogFormat("[Forget Them All #{0}] Wire cut order: {1}.", moduleId, ListToString(cutOrder));
        else
        {
            Debug.LogFormat("[Forget Them All #{0}] Remaining uncut wires have no characters in relation to the module name! Cut any wire to solve the module.", moduleId);
        }

    }

    int GetCharColor(char c)
    {
        char conv = char.ToUpper(c);
        switch (conv)
        {
            case 'A':
            case 'N':
            case '0':
                return 0;
            case 'B':
            case 'O':
            case '1':
                return 1;
            case 'C':
            case 'P':
            case '2':
                return 2;
            case 'D':
            case 'Q':
            case '3':
                return 3;
            case 'E':
            case 'R':
            case '4':
                return 4;
            case 'F':
            case 'S':
            case '5':
                return 5;
            case 'G':
            case 'T':
            case '6':
                return 6;
            case 'H':
            case 'U':
            case '7':
                return 7;
            case 'I':
            case 'V':
            case '8':
                return 8;
            case 'J':
            case 'W':
            case '9':
                return 9;
            case 'K':
            case 'X':
                return 10;
            case 'L':
            case 'Y':
                return 11;
            case 'M':
            case 'Z':
                return 12;
            default:
                return -1;
        }
    }

    string ListToString(List<int> l)
    {
        string res = "[";

        for (int i = 0; i < l.Count(); i++)
        {
            res += GetColorName(l[i]);
            if (i != l.Count() - 1)
                res += " ";
        }

        return res + "]";
    }

    public class ForgetThemAllSettings
    {
        public bool hardcoreBossModule = false;
    }
    IEnumerator TwitchHandleForcedSolve()
    {// Courtesy of eXish, modified by VFlyer
        Debug.LogFormat("[Forget Them All #{0}] A force solve has been issued via TP Handler.", moduleId);
        requestForceSolve = true;
        while (!readyToSolve)
        {
            yield return true;
        }
        int start = wiresCut.Count();
        int end = cutOrder.Count();
        List<int> uncutWires = Enumerable.Range(0, 13).Where(a => !wiresCut.Contains(a)).ToList();
        if (cutOrder.Any())
        {
            for (int i = start; i < end; i++)
            {
                wireInt[Array.IndexOf(colors, cutOrder[0])].OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            yield return true;
        }
        else
        {
            wireInt[uncutWires.PickRandom()].OnInteract();
            yield return true;
        }
        /**if (!moduleSolved)
			Debug.LogFormat("[Forget Them All #{0}] A force solve has been issued viva TP Handler.", moduleId);
		StartCoroutine(FakeSolveHandling());*/

    }

    private static readonly string[][] _wireColorNames = new string[13][]
    {
        new string[] { "yellow", "y" },
        new string[] { "grey", "gray", "a" },
        new string[] { "blue", "b" },
        new string[] { "green", "g" },
        new string[] { "orange", "o" },
        new string[] { "red", "r" },
        new string[] { "lime", "l" },
        new string[] { "cyan", "c" },
        new string[] { "brown", "n" },
        new string[] { "white", "w" },
        new string[] { "purple", "p" },
        new string[] { "magenta", "m" },
        new string[] { "pink", "i" }
    };

    
    private readonly string TwitchHelpMessage = "Cut the wires with \"!{0} cut 1 2 3 ...\", or with \"!{0} cut red orange yellow ...\". Wires are numbered 1–13 from left to right on the module. For color name abbreviations, use \"!{0} colornames\". To toggle colorblind mode: \"!{0} colorblind\"";
    
    IEnumerator ProcessTwitchCommand(string command) // TP Handler rewritten by Quinn Weast
    {
        var c = Regex.Match(command, @"^\s*colou?rblind", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (c.Success)
        {
            yield return null;
            colorblindDetected = !colorblindDetected;
            if (readyToSolve)
                ShowFinalStage();
            else
                DisplayCurrentStage(currentStage);
            yield break;
        }
        c = Regex.Match(command, @"^\s*colou?rnames", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (c.Success)
        {
            yield return null;
            yield return "sendtochat Color abbreviants: YELLOW/Y, GREY/GRAY/A, BLUE/B, ORANGE/O, RED/R, LIME/L, CYAN/C, BROWN/N, WHITE/W, PURPLE/P, MAGENTA/M, PINK/I.";
            yield break;
        }
        var list = new List<int>();
        var m = Regex.Match(command, @"^\s*cut((\s+\d+)+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            var vals = m.Groups[1].Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var n in vals)
            {
                int v;
                if (!int.TryParse(n, out v) || v < 1 || v > 13)
                {
                    yield return string.Format("sendtochaterror {0} is an invalid wire position! Wires are numbered 1-13 in order.", n);
                    yield break;
                }
                list.Add(v - 1);
            }
        }
        else
        {
            m = Regex.Match(command, string.Format(@"^\s*cut((\s+({0}))+)\s*$", _wireColorNames.SelectMany(i => i.Select(j => Regex.Escape(j))).Join("|")), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (m.Success)
            {
                var vals = m.Groups[1].Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var n in vals)
                {
                    int v = _wireColorNames.IndexOf(i => i.Contains(n, StringComparer.InvariantCultureIgnoreCase));
                    if (v == -1)
                    {
                        yield return string.Format("sendtochaterror {0} is an invalid wire color! Wire colors are: {1}", n, _wireColorNames.Select(i => i[0]).Join(", "));
                        yield break;
                    }
                    list.Add(Array.IndexOf(colors, v));
                }
            }
            else
                yield break;
        }
        if (list.Distinct().Count() != list.Count)
        {
            yield return "sendtochaterror You're trying to cut the same wire twice! Command stopped.";
            yield break;
        }
        var selList = new List<KMSelectable>();
        var consideredToStrike = false;
        for (int i = 0; i < list.Count; i++)
        {
            int wireix = list[i];
            if (!readyToSolve || (i < cutOrder.Count && cutOrder[i] != colors[wireix]))
            {
                consideredToStrike = true;
                yield return string.Format("strikemessage incorrectly cutting the {0} wire at position {1} after {2} previous cuts!", _wireColorNames[colors[wireix]][0], wireix + 1, i);
            }
            if (wiresCut.Contains(wireix))
            {
                yield return string.Format("sendtochaterror the {0} wire at position {1} has already been cut! Command stopped.", _wireColorNames[colors[wireix]][0], wireix + 1);
                yield break;
            }
            selList.Add(wireInt[wireix]);
            if (consideredToStrike)
                break;
        }
        yield return null;
        yield return consideredToStrike ? "strike" : "solve";
        foreach (var sel in selList)
        {
            sel.OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        yield break;
    }
}
