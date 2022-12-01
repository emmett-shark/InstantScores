using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace InstantScores;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public static Plugin Instance;
    public static ManualLogSource Log;

    private void Awake()
    {
        Instance = this;
        Log = Logger;
        new Harmony(PluginInfo.PLUGIN_GUID).PatchAll();
    }
}

[HarmonyPatch(typeof(PointSceneController), nameof(PointSceneController.Start))]
public class PointSceneControllerStartPatch : MonoBehaviour
{
    public static Dictionary<string, int> Scores = new Dictionary<string, int> {
        { "F", 0 }, { "D", 1 }, { "C", 2 }, { "B", 3 }, { "A", 4 }, { "S", 5 }
    };
    
    static bool Prefix(PointSceneController __instance, ref AudioSource[] ___sfx)
    {
        Plugin.Log.LogDebug("Starting PointSceneController");
        ___sfx = __instance.sfxholder.GetComponents<AudioSource>();

        int totalscore = GlobalVariables.gameplay_scoretotal;
        float scorepercentage = GlobalVariables.gameplay_scoreperc;
        string letterscore = getLetterScore(scorepercentage);
        int scoreindex = Scores[letterscore];
        int coins = getTootsNum(scorepercentage);

        __instance.txt_trackname.text = GlobalVariables.chosen_track_data.trackname_long;
        __instance.txt_prevhigh.text = SaverLoader.grabHighestScore(GlobalVariables.chosen_track_data.trackref).ToString("n0");
        __instance.scorecountertext.text = totalscore.ToString("n0");
        __instance.giantscoretext.text = letterscore;
        __instance.giantscoretextshad.text = letterscore;
        __instance.tootstext.text = "EARNED " + coins.ToString() + " TOOTS";

        __instance.txt_nasties.text = GlobalVariables.gameplay_notescores[0].ToString("n0");
        __instance.txt_mehs.text = GlobalVariables.gameplay_notescores[1].ToString("n0");
        __instance.txt_okays.text = GlobalVariables.gameplay_notescores[2].ToString("n0");
        __instance.txt_nices.text = GlobalVariables.gameplay_notescores[3].ToString("n0");
        __instance.txt_perfectos.text = GlobalVariables.gameplay_notescores[4].ToString("n0");

        setTrackPositions(__instance, scorepercentage, scoreindex);
        __instance.wallbreak.SetActive(scorepercentage > 1.35f);
        __instance.screenfade.SetActive(false); // otherwise this makes a weird diagonal line

        SaverLoader.checkForUpdatedScore(GlobalVariables.chosen_track_data.trackref, totalscore, letterscore);
        GlobalVariables.localsave.tracks_played++;
        GlobalVariables.localsave.currency_toots += coins;
        SaverLoader.updateSavedGame();

        if (scoreindex > 3)
        {
            __instance.confettic.setUpConfetti();
            __instance.confettic.startConfetti();
        }

        __instance.GetType().GetMethod("startAnims", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { });
        __instance.GetType().GetMethod("checkScoreCheevos", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { });
        //highscore accuracy mod uses this
        __instance.GetType().GetMethod("doneWithCountUp", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { });
        return false;
    }

    private static float trackdotspacing = 89.5f;
    private static float trackypos = 99f;
    private static float trackxpos = 179f;
    private static float trackdotsize = 20f;
    private static float trackbarheight = 6f;
    private static float trackfullwidth = trackdotspacing * 5f;
    private static float num = trackdotsize * 0.5f;

    private static void setTrackPositions(PointSceneController __instance, float scorepercentage, int scoreindex)
    {
        var track_barempty = getChild<RectTransform>(__instance.trackobj.transform, 0);
        var track_barfill = getChild<RectTransform>(__instance.trackobj.transform, 1);
        var track_arrow = getChild<RectTransform>(__instance.trackobj.transform, 2);

        for (int i = 0; i < Scores.Count; i++)
        {
            var trackDot = getChild<RectTransform>(__instance.trackobj.transform, i + 3);
            trackDot.anchoredPosition3D = getDotSpacing(i);
            if (i <= scoreindex)
            {
                var image = getChild<Image>(__instance.trackobj.transform, i + 3);
                image.color = new Color32(byte.MaxValue, byte.MaxValue, 0, byte.MaxValue);
                var text = getChild<Text>(trackDot.transform, 0);
                text.color = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
            }
        }

        track_arrow.anchoredPosition3D = new Vector3(trackxpos - 1f + trackfullwidth * scorepercentage, -trackypos, 0.0f);

        track_barfill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, trackfullwidth * scorepercentage);
        track_barfill.anchoredPosition3D = new Vector3(trackxpos, (float)(-trackypos + trackbarheight * 0.5), 0.0f);

        track_barempty.anchoredPosition3D = new Vector3(trackxpos, (float)(-trackypos + trackbarheight * 0.5), 0.0f);
        track_barempty.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, trackfullwidth);
    }

    private static T getChild<T>(Transform transform, int i) => transform.GetChild(i).gameObject.GetComponent<T>();

    private static Vector3 getDotSpacing(int i) => new Vector3((float)(trackxpos - num + trackdotspacing * i), -trackypos + num, 0.0f);

    private static string getLetterScore(float scorepercentage) => scorepercentage < 1f ? (scorepercentage < 0.8f ? (scorepercentage < 0.6f ? (scorepercentage < 0.4f ? (scorepercentage < 0.2f ? "F" : "D") : "C") : "B") : "A") : "S";
    
    private static int getTootsNum(float scorepercentage)
    {
        int difficulty = GlobalVariables.chosen_track_data.difficulty;
        int length = GlobalVariables.chosen_track_data.length;
        float difficultyMod = difficulty > 4 ? 1.04f : 1f;
        float lengthMod = length > 90 ? (length - 90) * 0.007f + 1f : 1f;
        int total = Mathf.Max(0, Mathf.FloorToInt(scorepercentage * 375f * difficultyMod * lengthMod));
        Plugin.Log.LogDebug($"new toots: {total}");
        return total;
    }
}

[HarmonyPatch(typeof(PointSceneController), nameof(PointSceneController.doneWithCountUp))]
public class PointSceneControllerDoneWithCountUpPatch : MonoBehaviour
{
    static bool Prefix() => false;
}
