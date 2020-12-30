    using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Mono.Cecil.Cil;
using Poly.Physics;
using PolyPhysics;
using PolyTechFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ColoredEdges
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(PolyTechFramework.PolyTechMain.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInProcess("Poly Bridge 2.exe")]
    public class PluginMain : PolyTechMod
    {
        public const String PluginGuid = "polytech.colorededges";
        public const String PluginName = "Colored Bridge Pieces";
        public const String PluginVersion = "1.5.0";

        private static BepInEx.Logging.ManualLogSource staticLogger;

        public static ConfigEntry<int> TotalHotkeys;
        public static ConfigEntry<float> GamingSpeedMultiplier;
        public static ConfigEntry<float> GamingDistanceXMultiplier;
        public static ConfigEntry<float> GamingDistanceYMultiplier;
        public static ConfigEntry<KeyboardShortcut>[] ColorHotkeys;
        public static ConfigEntry<String>[] ColorStrings;
        public static ConfigEntry<bool> ColorHydraulicPistons;
        public static ConfigEntry<bool> ColorHydraulicSleeve;
        public static Color[] ColorArr;

        public static Color defaultHydraulicSleeveColor = new Color(0.1607843f, 0.3176471f, 0.6705883f, 1f);
        public static Color defaultHydraulicPistonColor = new Color(0.5607843f, 0.7254902f, 0.9137255f, 1f);

        public static Color randomColor = new Color(420f, 69f, 926f, 621f);
        public static Color cycleRGB = new Color(100f, 100f, 100f, 100f);
        //R O Y G B V RANDOM
        public static Color[] defaultColors = {
            Color.red, new Color(255f/255f,165f/255f,0f), Color.yellow, Color.green, Color.blue, new Color(127f/255f,0f,255f/255f), randomColor, cycleRGB
        };

        public static System.Random random = new System.Random();

        public static List<BridgeEdgeProxy> bridgeEdgeProxyList = new List<BridgeEdgeProxy>();
        public static List<Color> bridgeEdgeColorList = new List<Color>();
        public static Color springDefault = new Color(0f, 1f, 1f, 1f);
        public static int edgesLeftToCreateAfterThemeChange = 0;
        public static Color nextDebrisColor;
        public static Color[] nextDebrisGamerColor;

        public static int hotkeysToResetTo = 10;
        
        public static int hotkeyIndexDown = -1;

        void Awake()
        {
            staticLogger = Logger;

            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            SetupTotalHotkeys();
            SetupSettings();

            PolyTechMain.registerMod(this);
        }

        void Update()
        {
            hotkeyIndexDown = -1;
            int totalkeys = TotalHotkeys.Value;
            for (int i=0; i < totalkeys; i++)
            {
                //Logger.LogMessage(i);
                if (ColorHotkeys[i % totalkeys].Value.IsPressed())
                {
                    hotkeyIndexDown = i;
                    break;
                }
            }
            //staticLogger.LogMessage("hotkeyIndexDown: "+ hotkeyIndexDown);
        }

        void SetupTotalHotkeys()
        {
            TotalHotkeys = Config.Bind(Convert.ToChar(0x356) + "*General*", "Amount Of Keybinds (Needs Menu Reload)", hotkeysToResetTo, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 0 }));

            ColorHydraulicPistons = Config.Bind(Convert.ToChar(0x356) + "*General*", "Color Hydraulic Pistons", false, new ConfigDescription("Toggles coloring the hydraulic piston.", null, new ConfigurationManagerAttributes { Order = 0 }));
            ColorHydraulicSleeve = Config.Bind(Convert.ToChar(0x356) + "*General*", "Color Hydraulic Sleeve", true, new ConfigDescription("Toggles coloring the hydraulic sleeve.", null, new ConfigurationManagerAttributes { Order = 0 }));

            GamingSpeedMultiplier = Config.Bind(Convert.ToChar(0x356) + "*General*", "RGB Speed Multiplier", 1f, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 0 }));
            GamingDistanceXMultiplier = Config.Bind(Convert.ToChar(0x356) + "*General*", "RGB X Distance Multiplier", 1f, new ConfigDescription("The difference between the colors of farther away materials (Use 0 to disable)", null, new ConfigurationManagerAttributes { Order = 0 }));
            GamingDistanceYMultiplier = Config.Bind(Convert.ToChar(0x356) + "*General*", "RGB Y Distance Multiplier", 1f, new ConfigDescription("The difference between the colors of farther away materials (Use 0 to disable)", null, new ConfigurationManagerAttributes { Order = 0 }));
            
            TotalHotkeys.SettingChanged += (o, e) =>
            {
                Logger.LogMessage("Resetting to " + TotalHotkeys.Value + " hotkeys");
                try
                {
                    hotkeysToResetTo = TotalHotkeys.Value; //Get around recursion

                    String[] colorStrs = new String[TotalHotkeys.Value];
                    KeyboardShortcut[] keyboardShortcuts = new KeyboardShortcut[TotalHotkeys.Value];

                    for (int i = 0; i < TotalHotkeys.Value; i++)
                    {
                        if (i < ColorHotkeys.Count())
                        {
                            colorStrs[i] = ColorStrings[i].Value;
                            keyboardShortcuts[i] = ColorHotkeys[i].Value;
                        }
                    }

                    Config.Clear();
                    SetupTotalHotkeys();
                    SetupSettings();

                    for (int i = 0; i < TotalHotkeys.Value; i++)
                    {
                        ColorStrings[i].Value = colorStrs[i];
                        ColorHotkeys[i].Value = keyboardShortcuts[i];
                    }
                }
                catch(Exception f) 
                {
                    Logger.LogError("An error has occured with resetting the hotkeys.");
                }
            };
        }
        void SetupSettings()
        {
            ColorHotkeys = new ConfigEntry<KeyboardShortcut>[TotalHotkeys.Value];
            ColorStrings = new ConfigEntry<String>[TotalHotkeys.Value];
            ColorArr = new Color[TotalHotkeys.Value];

            for (int colorCountIndex = 0; colorCountIndex < TotalHotkeys.Value; colorCountIndex++)
            {
                var currentIndex = colorCountIndex;
                Color defaultColor = Color.white;
                String key = "Color " + (colorCountIndex + 1);

                //Add accent character so it orders correctly: P
                if (TotalHotkeys.Value >= 10 && colorCountIndex < 9)
                {
                    key = Convert.ToChar(0x356) + key;
                }

                //Change the default color if it's less than the length of default colors
                if (colorCountIndex < defaultColors.Length)
                {
                    defaultColor = defaultColors[colorCountIndex];
                }

                //Bind configs
                ColorHotkeys[colorCountIndex] = Config.Bind(key, "Color " + (colorCountIndex + 1) + " Keybind", new KeyboardShortcut(KeyCode.None), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = currentIndex }));
                ColorStrings[colorCountIndex] = Config.Bind(key, "Color " + (colorCountIndex + 1) + " Hex Value", ColorToHexString(defaultColor), new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = currentIndex }));
                int val = colorCountIndex; //Necessary to do else the function will only have TotalHotkeys.Value
                ColorStrings[colorCountIndex].SettingChanged += (o, e) => { UpdateColor(val); };
                UpdateColor(val);
            }
        }

        void UpdateColor(int index)
        {
            //staticLogger.LogMessage("Updating color "+index);
            Color color;

            //Check if the color is "random"
            if (ColorStrings[index].Value.ToLower().Equals("random"))
            {
                ColorArr[index] = randomColor;
            }
            if (ColorStrings[index].Value.ToLower().Equals("gamer"))
            {
                ColorArr[index] = cycleRGB;
            }
            //Use the same system that custom shapes uses for consistency
            else if (ColorUtility.TryParseHtmlString(ColorStrings[index].Value, out color))
            {
                ColorArr[index] = color;
            }
        }

        string ColorToHexString(Color c)
        {
            if (c == randomColor) { return "Random"; }
            else if (c == cycleRGB) { return "Gamer"; }
            else
            {
                byte[] arr = { ((byte)(c.r * 255)), ((byte)(c.g * 255)), ((byte)(c.b * 255)) };
                return "#" + BitConverter.ToString(arr).Replace("-", ""); //BitConverter adds -'s so we have to remove them
            }
        }

        //epico https://stackoverflow.com/questions/2288498/how-do-i-get-a-rainbow-color-gradient-in-c
        public static Color Rainbow(float progress)
        {
            float div = (Math.Abs(progress % 1) * 6);
            float ascending = (int)((div % 1) * 255);
            float descending = 255 - ascending;

            switch ((int)div)
            {
                case 0:
                    return new Color(255f/255f, 255f/255f, ascending/255f, 0f / 255f);
                case 1:
                    return new Color(255f/255f, descending/255f, 255f/255f, 0f / 255f);
                case 2:
                    return new Color(255f/255f, 0f/255f, 255f/255f, ascending / 255f);
                case 3:
                    return new Color(255f/255f, 0f/255f, descending/255f, 255f / 255f);
                case 4:
                    return new Color(255f/255f, ascending/255f, 0f/255f, 255f / 255f);
                default: // case 5:
                    return new Color(255f/255f, 255f/255f, 0f/255f, descending / 255f);
            }
        }

        [HarmonyPatch(typeof(BridgeEdges), "CreateEdge")]
        static class Patch_BridgeEdges_CreateEdge
        {
            [HarmonyPostfix]
            static void Postfix(ref BridgeEdge __result)
            {
                if (__result)
                {
                    //staticLogger.LogMessage("edgesLeftToCreateAfterThemeChange " + edgesLeftToCreateAfterThemeChange);
                    if (GameStateManager.GetState() != GameState.SIM && (edgesLeftToCreateAfterThemeChange<=0))
                    {
                        if (hotkeyIndexDown != -1)
                        {
                            Color c = ColorArr[hotkeyIndexDown];
                            if (c == randomColor)
                            {
                                c = new UnityEngine.Color((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
                                staticLogger.LogMessage("Generated " + __result.m_Material.m_MaterialType.ToString() + " with random color: " + c);

                            }
                            else if (c == springDefault)
                            {
                                c.a -= 0.001f;
                            }
                            if (c != cycleRGB || __result.m_OriginalColors != null)
                            {
                                SetMaterialColor(__result, c);
                            }
                            else
                            {
                                CreateRainbowStartingPoint(__result);
                            }
                        }
                        //else
                        //{
                        //    //staticLogger.LogMessage(__result.m_Material.m_MaterialType + " Color: " + __result.m_MeshRenderer.material.color);
                        //}
                    }
                    else
                    {
                        //Runs when re-creating colored edges when switching theme / going back to editor
                        for (int i = 0; i < bridgeEdgeProxyList.Count; i++)
                        {
                            if (bridgeEdgeProxyList[i].m_NodeA_Guid.Equals(__result.m_JointA.m_Guid) && bridgeEdgeProxyList[i].m_NodeB_Guid.Equals(__result.m_JointB.m_Guid))
                            {
                                Color colorFromList = bridgeEdgeColorList[i];
                                SetMaterialColor(__result, colorFromList);
                                if (colorFromList == cycleRGB)
                                {
                                    CreateRainbowStartingPoint(__result);
                                    SetMaterialColor(__result, Rainbow(__result.m_OriginalColors[0].a));
                                }
                            }
                        }
                        edgesLeftToCreateAfterThemeChange--;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(BridgeEdge), "UpdateManual")]
        static class Patch_BridgeEdge_UpdateManual
        {
            [HarmonyPostfix]
            static void Postfix(ref BridgeEdge __instance)
            {
                if (__instance.m_OriginalColors != null)
                {
                    __instance.m_OriginalColors[0].a += 0.001f*GamingSpeedMultiplier.Value;
                    SetMaterialColor(__instance, Rainbow(__instance.m_OriginalColors[0].a));
                }
            }
        }

        [HarmonyPatch(typeof(BridgeEdgeListener), "CreateDebris")]
        static class Patch_BridgeEdgeListener_CreateDebris 
        {
            [HarmonyPrefix]
            static void Prefix(ref EdgeHandle e, ref BridgeEdge brokenEdge)
            {
                //Set the color of the debris edge
                nextDebrisGamerColor = null;
                nextDebrisColor = brokenEdge.m_MeshRenderer.material.color;
                if (brokenEdge.m_OriginalColors != null) 
                {
                    nextDebrisGamerColor = brokenEdge.m_OriginalColors;
                }
            }
        }

        [HarmonyPatch(typeof(BridgeEdgeListener), "CreateBridgeEdgeFromEdge")]
        static class Patch_BridgeEdgeListener_CreateBridgeEdgeFromEdge
        {
            [HarmonyPostfix]
            static void Postfix(ref BridgeEdge __result)
            {
                //Get the color for the next debris edge
                __result.m_MeshRenderer.material.color = nextDebrisColor;
                if (nextDebrisGamerColor != null)
                {
                    __result.m_OriginalColors = nextDebrisGamerColor;
                }
            }
        }

        [HarmonyPatch(typeof(BridgeSprings), "CreateSpring")]
        static class Patch_BridgeSprings_CreateSpring
        {
            [HarmonyPostfix]
            static void Postfix(ref BridgeSpring __result)
            {
                //Set the spring color
                if (__result)
                {
                    SetMaterialColor(__result, __result.m_ParentEdge.m_MeshRenderer.material.color);
                }
            }
        }

        [HarmonyPatch(typeof(BridgeSpring), "CreateLink")]
        static class Patch_BridgeSpring_CreateLink
        {
            [HarmonyPostfix]
            static void Postfix(ref BridgeSpring __instance, ref BridgeSpringLink __result)
            {
                if (__instance)
                {
                    if (__instance.m_ParentEdge.m_MeshRenderer.material.color != springDefault)
                    {
                        __result.m_MeshRenderer.material.color = __instance.m_ParentEdge.m_MeshRenderer.material.color;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(BridgeRopes), "Add")]
        static class Patch_BridgeRopes_Add
        {
            [HarmonyPostfix]
            static void Postfix()
            {
                if (BridgeRopes.m_BridgeRopes.Last<BridgeRope>() != null)
                {
                    SetMaterialColor(BridgeRopes.m_BridgeRopes.Last<BridgeRope>(), BridgeRopes.m_BridgeRopes.Last<BridgeRope>().m_ParentEdge.m_MeshRenderer.material.color);
                }
            }
        }

        [HarmonyPatch(typeof(BridgeEdges), "Serialize")]
        static class Patch_BridgeEdges_Serialize
        {
            [HarmonyPostfix]
            static void Postfix()
            {
                bridgeEdgeProxyList.Clear();
                bridgeEdgeColorList.Clear();
                foreach (BridgeEdge edge in BridgeEdges.m_Edges)
                {
                    if (edge)
                    {
                        bridgeEdgeProxyList.Add(new BridgeEdgeProxy(edge));

                        Color c = edge.m_MeshRenderer.material.color;
                        if (edge.m_OriginalColors != null) { c = cycleRGB; }
                        if (edge.m_Material.m_MaterialType == BridgeMaterialType.HYDRAULICS)
                        {
                            if (ColorHydraulicSleeve.Value)
                            {
                                c = edge.m_HydraulicEdgeVisualization.GetComponentsInChildren<MeshRenderer>()[0].material.color;
                            }
                        }


                        bridgeEdgeColorList.Add(c);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Panel_SandboxSettings), "OnThemeChanged")]
        static class Patch_Panel_SandboxSettings_OnThemeChanged
        {
            [HarmonyPrefix]
            static void Prefix()
            {
                edgesLeftToCreateAfterThemeChange = BridgeEdges.m_Edges.Count;
                //staticLogger.LogMessage("edgesLeftToCreateAfterThemeChange set to " + edgesLeftToCreateAfterThemeChange);
            }
        }

        [HarmonyPatch(typeof(ClipboardManager), "MaybeCopyEdgeJointSelections")]
        static class Patch_ClipboardManager_MaybeCopyEdgeJointSelections
        {
            [HarmonyPrefix]
            static void Prefix(BridgeEdge newEdge, BridgeEdge sourceEdge)
            {
                Color c = sourceEdge.m_MeshRenderer.material.color;
                if (sourceEdge.m_OriginalColors != null) { c = cycleRGB; }

                SetMaterialColor(newEdge, c); //Set copy paste material color if no color key is held down
            }
        }

        [HarmonyPatch(typeof(ClipboardManager), "AddEdge")] 
        static class Patch_ClipboardManager_AddEdge //Sets the Copy/Paste preview colors to the material colors
        {
            [HarmonyPostfix]
            static void Postfix(ref List<ClipboardEdge> ___m_Edges, ref GameObject ___m_ClipboardContainer)
            {
                Color color = ___m_Edges.Last<ClipboardEdge>().m_SourceBridgeEdge.m_MeshRenderer.material.color;

                if (!(___m_Edges.Last<ClipboardEdge>().m_SourceBridgeEdge.m_Material.m_MaterialType == BridgeMaterialType.SPRING && color == springDefault))
                {
                    ___m_ClipboardContainer.GetComponentsInChildren<MeshRenderer>().Last<MeshRenderer>().material.color = color;
                }
            }
        }

        [HarmonyPatch(typeof(ClipboardManager), "PasteSpring")]
        static class Patch_ClipboardManager_PasteSpring //Sets the Copy/Paste preview colors to the material colors
        {
            [HarmonyPrefix]
            static void Prefix(ref BridgeEdge newEdge, ref BridgeEdge sourceEdge)
            {
                newEdge.m_MeshRenderer.material.color = sourceEdge.m_MeshRenderer.material.color;
            }
        }

        [HarmonyPatch(typeof(BridgeRope), "UpdateManual")]
        static class Patch_BridgeRope_UpdateManual //Update rope colors for gamer mode
        {
            [HarmonyPrefix]
            static void Prefix(ref BridgeRope __instance)
            {
                if (__instance.m_ParentEdge.m_OriginalColors != null)
                {
                    SetMaterialColor(__instance, __instance.m_ParentEdge.m_MeshRenderer.material.color);
                }
            }
        }

        [HarmonyPatch(typeof(BridgeSpring), "UpdateManual")]
        static class Patch_BridgeSpring_UpdateManual //Update spring colors for gamer mode
        {
            [HarmonyPrefix]
            static void Prefix(ref BridgeSpring __instance)
            {
                if (__instance.m_ParentEdge.m_OriginalColors != null)
                {
                    SetMaterialColor(__instance, __instance.m_ParentEdge.m_MeshRenderer.material.color);
                }
            }
        }

        static void SetMaterialColor(BridgeEdge edge, Color c)
        {
            if (edge.m_Material.m_MaterialType == BridgeMaterialType.SPRING && edge.m_SpringCoilVisualization && edge.m_MeshRenderer.material.color != springDefault)
            {
                edge.m_SpringCoilVisualization.m_FrontLink.m_MeshRenderer.material.color = c;
                edge.m_SpringCoilVisualization.m_BackLink.m_MeshRenderer.material.color = c;
            }
            if (c == cycleRGB)
            {
                CreateRainbowStartingPoint(edge);
            }
            if (edge.m_Material.m_MaterialType == BridgeMaterialType.HYDRAULICS)
            {
                if (ColorHydraulicPistons.Value)
                {
                    edge.m_MeshRenderer.material.color = c;
                }
                if (ColorHydraulicSleeve.Value)
                {
                    edge.CreateHydraulicVisualization();
                    edge.m_HydraulicEdgeVisualization.SetColor(c);
                }
            }
            else
            {
                edge.m_MeshRenderer.material.color = c;
            }
            //staticLogger.LogMessage("Set "+ edge.m_Material.m_MaterialType.ToString() + " edge color to: " + c);
        }

        static void SetMaterialColor(BridgeRope rope, Color c)
        {
            rope.m_PhysicsRope.lineMaterial.color = c;
            
            foreach(BridgeLink link in rope.m_Links)
            {
                link.m_Link.GetComponent<MeshRenderer>().material.color = c;
            }
            //staticLogger.LogMessage("Set " + rope.m_ParentEdge.m_Material.m_MaterialType.ToString() + " rope color to: " + c);
        }

        static void SetMaterialColor(BridgeSpring spring, Color c)
        {
            if (c != springDefault)
            {
                spring.m_ParentEdge.m_MeshRenderer.material.color = c;
                spring.m_FrontLink.m_MeshRenderer.material.color = c;
                spring.m_BackLink.m_MeshRenderer.material.color = c;
            }
        }

        public static void CreateRainbowStartingPoint(BridgeEdge edge)
        {
            var aPos = edge.m_JointA.m_BuildPos;
            var bPos = edge.m_JointB.m_BuildPos;

            var midpoint = Vector3.Lerp(aPos, bPos, 0.5f);

            float ret = midpoint.x*GamingDistanceXMultiplier.Value + midpoint.y*GamingDistanceYMultiplier.Value;

            edge.m_OriginalColors = new Color[] { new Color(0, 0, 0, ret*.1f) };
        }
    }
}
