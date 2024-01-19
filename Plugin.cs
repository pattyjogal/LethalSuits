using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using UnityEngine;

namespace LethalSharedSuits
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class MoreSuitsMod : BaseUnityPlugin
    {
        private const string modGUID = "pattyjogal.LethalSuits";
        private const string modName = "Lethal Suits";
        private const string modVersion = "0.1.0";
        private static readonly string modBasePath = Path.Combine(Paths.PluginPath, "lethal-suits");
        private static readonly string suitsBasePath = Path.Combine(modBasePath, "suits");
        private static readonly string friendsSuitsBasePath = Path.Combine(modBasePath, "friends");

        private readonly Harmony harmony = new Harmony(modGUID);

        private static MoreSuitsMod Instance;

        public static bool SuitsAdded = false;

        public static string DisabledSuits;
        public static bool LoadAllSuits;
        public static bool MakeSuitsFitOnRack;
        public static int MaxSuits;

        public static List<Material> customMaterials = new List<Material>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            DisabledSuits = Config.Bind("General", "Disabled Suit List", "UglySuit751.png,UglySuit752.png,UglySuit753.png", "Comma-separated list of suits that shouldn't be loaded").Value;
            LoadAllSuits = Config.Bind("General", "Ignore !less-suits.txt", false, "If true, ignores the !less-suits.txt file and will attempt to load every suit, except those in the disabled list. This should be true if you're not worried about having too many suits.").Value;
            MakeSuitsFitOnRack = Config.Bind("General", "Make Suits Fit on Rack", true, "If true, squishes the suits together so more can fit on the rack.").Value;
            MaxSuits = Config.Bind("General", "Max Suits", 100, "The maximum number of suits to load. If you have more, some will be ignored.").Value;

            harmony.PatchAll();
            Logger.LogInfo($"Plugin {modName} is loaded!");
        }

        [HarmonyPatch(typeof(StartOfRound))]
        internal class StartOfRoundPatch
        {
            private static UnlockableItem GetOriginalSuit(ref StartOfRound __instance)
            {
                return __instance.unlockablesList.unlockables.First(unlockable => unlockable.suitMaterial != null && unlockable.alreadyUnlocked);
            }

            private static List<string> GetSuitsFromConfig(string configPath)
            {
                return File.ReadAllText(configPath).Split(new string[] { Environment.NewLine }, StringSplitOptions.None).ToList();
            }

            private static List<string> GetUninstalledSuits(List<string> allSuits) {
                return allSuits.Except(Directory.GetDirectories(suitsBasePath)).ToList();
            }

            private static void DownloadSuit(string suit)
            {
                string downloadUrl = $"https://lethal-suits.nyc3.digitaloceanspaces.com/{suit}.zip";
                string zipFilePath = Path.Combine(suitsBasePath, $"{suit}.zip");
                string extractedPath = Path.Combine(suitsBasePath, suit);
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFile(downloadUrl, Path.Combine(suitsBasePath, zipFilePath));
                    }
                    ZipFile.ExtractToDirectory(zipFilePath, extractedPath);
                }
                catch (WebException e)
                {
                    Debug.Log($"Error downloading the ZIP file: {e.Message}");
                }
                catch (IOException e)
                {
                    Debug.Log($"Error extracting the ZIP file: {e.Message}");
                }
            }

            [HarmonyPatch("Start")]
            [HarmonyPrefix]
            static void StartPatch(ref StartOfRound __instance)
            {
                int originalUnlockablesCount = __instance.unlockablesList.unlockables.Count;
                UnlockableItem originalSuit = new UnlockableItem();
                try
                {
                    if (!Directory.Exists(modBasePath))
                    {
                        Directory.CreateDirectory(modBasePath);
                    }

                    if (!Directory.Exists(suitsBasePath))
                    {
                        Directory.CreateDirectory(suitsBasePath);
                    }

                    if (!Directory.Exists(friendsSuitsBasePath))
                    {
                        Directory.CreateDirectory(friendsSuitsBasePath);
                    }

                    if (!SuitsAdded) // we only need to add the new suits to the unlockables list once per game launch
                    {
                        int addedSuitCount = 0;
                        originalSuit = GetOriginalSuit(ref __instance);

                        // Install any suits not currently present
                        List<string> configs = [.. Directory.GetFiles(friendsSuitsBasePath)];
                        configs.Add(Path.Combine(modBasePath, "suits.txt"));
                        List<string> configuredSuits = configs.SelectMany(GetSuitsFromConfig).ToList();
                        List<string> suitsToInstall = GetUninstalledSuits(configuredSuits);
                        suitsToInstall.ForEach(Debug.Log);
                        suitsToInstall.ForEach(DownloadSuit);

                        // Get all .png files from all folders named moresuits in the BepInEx/plugins folder
                        List<string> texturePaths = new List<string>();
                        List<string> assetPaths = new List<string>();

                        
                        foreach (string suitsFolderPath in configuredSuits.Select(suit => Path.Combine(suitsBasePath, suit)))
                        {
                            if (suitsFolderPath != "")
                            {
                                string[] pngFiles = Directory.GetFiles(suitsFolderPath, "*.png");
                                string[] bundleFiles = Directory.GetFiles(suitsFolderPath, "*.matbundle");

                                texturePaths.AddRange(pngFiles);
                                assetPaths.AddRange(bundleFiles);
                            }
                        }

                        assetPaths.Sort();
                        texturePaths.Sort();

                        try
                        {
                            foreach (string assetPath in assetPaths)
                            {
                                AssetBundle assetBundle = AssetBundle.LoadFromFile(assetPath);
                                UnityEngine.Object[] assets = assetBundle.LoadAllAssets();

                                foreach (UnityEngine.Object asset in assets)
                                {
                                    if (asset is Material)
                                    {
                                        Material material = (Material)asset;
                                        customMaterials.Add(material);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.Log("Something went wrong with More Suits! Could not load materials from asset bundle(s). Error: " + ex);
                        }

                        // Create new suits for each .png
                        foreach (string texturePath in texturePaths)
                        {
                            // skip each suit that is in the disabled suits list
                            string originalMoreSuitsPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                            UnlockableItem newSuit;
                            Material newMaterial;

                            if (Path.GetFileNameWithoutExtension(texturePath).ToLower() == "default")
                            {
                                newSuit = originalSuit;
                                newMaterial = newSuit.suitMaterial;
                            }
                            else
                            {
                                // Serialize and deserialize to create a deep copy of the original suit item
                                newSuit = JsonUtility.FromJson<UnlockableItem>(JsonUtility.ToJson(originalSuit));

                                newMaterial = Instantiate(newSuit.suitMaterial);
                            }

                            byte[] fileData = File.ReadAllBytes(texturePath);
                            Texture2D texture = new Texture2D(2, 2);
                            texture.LoadImage(fileData);

                            newMaterial.mainTexture = texture;

                            newSuit.unlockableName = Path.GetFileNameWithoutExtension(texturePath);

                            // Optional modification of other properties like normal maps, emission, etc
                            // https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@14.0/manual/Lit-Shader.html
                            try
                            {
                                string advancedJsonPath = Path.Combine(Path.GetDirectoryName(texturePath), "advanced", newSuit.unlockableName + ".json");
                                if (File.Exists(advancedJsonPath))
                                {
                                    string[] lines = File.ReadAllLines(advancedJsonPath);

                                    foreach (string line in lines)
                                    {
                                        string[] keyValue = line.Trim().Split(':');
                                        if (keyValue.Length == 2)
                                        {
                                            string keyData = keyValue[0].Trim('"', ' ', ',');
                                            string valueData = keyValue[1].Trim('"', ' ', ',');

                                            if (valueData.Contains(".png"))
                                            {
                                                string advancedTexturePath = Path.Combine(Path.GetDirectoryName(texturePath), "advanced", valueData);
                                                byte[] advancedTextureData = File.ReadAllBytes(advancedTexturePath);
                                                Texture2D advancedTexture = new Texture2D(2, 2);
                                                advancedTexture.LoadImage(advancedTextureData);

                                                newMaterial.SetTexture(keyData, advancedTexture);
                                            }
                                            else if (keyData == "PRICE" && int.TryParse(valueData, out int intValue)) // If the advanced json has a price, set it up so it rotates into the shop
                                            {
                                                try
                                                {
                                                    newSuit = AddToRotatingShop(newSuit, intValue, __instance.unlockablesList.unlockables.Count);
                                                }
                                                catch (Exception ex)
                                                {
                                                    Debug.Log("Something went wrong with More Suits! Could not add a suit to the rotating shop. Error: " + ex);
                                                }
                                            }
                                            else if (valueData == "KEYWORD")
                                            {
                                                newMaterial.EnableKeyword(keyData);
                                            }
                                            else if (valueData == "DISABLEKEYWORD")
                                            {
                                                newMaterial.DisableKeyword(keyData);
                                            }
                                            else if (valueData == "SHADERPASS")
                                            {
                                                newMaterial.SetShaderPassEnabled(keyData, true);
                                            }
                                            else if (valueData == "DISABLESHADERPASS")
                                            {
                                                newMaterial.SetShaderPassEnabled(keyData, false);
                                            }
                                            else if (keyData == "SHADER")
                                            {
                                                Shader newShader = Shader.Find(valueData);
                                                newMaterial.shader = newShader;
                                            }
                                            else if (keyData == "MATERIAL")
                                            {
                                                foreach (Material material in customMaterials)
                                                {
                                                    if (material.name == valueData)
                                                    {
                                                        newMaterial = Instantiate(material);
                                                        newMaterial.mainTexture = texture;
                                                        break;
                                                    }
                                                }
                                            }
                                            else if (float.TryParse(valueData, out float floatValue))
                                            {
                                                newMaterial.SetFloat(keyData, floatValue);
                                            }
                                            else if (TryParseVector4(valueData, out Vector4 vectorValue))
                                            {
                                                newMaterial.SetVector(keyData, vectorValue);
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.Log("Something went wrong with More Suits! Error: " + ex);
                            }

                            newSuit.suitMaterial = newMaterial;

                            if (newSuit.unlockableName.ToLower() != "default")
                            {
                                if (addedSuitCount == MaxSuits)
                                {
                                    Debug.Log("Attempted to add a suit, but you've already reached the max number of suits! Modify the config if you want more.");
                                }
                                else
                                {
                                    __instance.unlockablesList.unlockables.Add(newSuit);
                                    addedSuitCount++;
                                }
                            }
                        }

                        SuitsAdded = true;
                    }

                    UnlockableItem dummySuit = JsonUtility.FromJson<UnlockableItem>(JsonUtility.ToJson(originalSuit));
                    dummySuit.alreadyUnlocked = false;
                    dummySuit.hasBeenMoved = false;
                    dummySuit.placedPosition = Vector3.zero;
                    dummySuit.placedRotation = Vector3.zero;
                    dummySuit.unlockableType = 753; // this unlockable type is not used
                    while (__instance.unlockablesList.unlockables.Count < originalUnlockablesCount + MaxSuits)
                    {
                        __instance.unlockablesList.unlockables.Add(dummySuit);
                    }

                }
                catch (Exception ex)
                {
                    Debug.Log("Something went wrong with More Suits! Error: " + ex);
                }

            }

            [HarmonyPatch("PositionSuitsOnRack")]
            [HarmonyPrefix]
            static bool PositionSuitsOnRackPatch(ref StartOfRound __instance)
            {
                List<string> mySuits = GetSuitsFromConfig(Path.Combine(modBasePath, "suits.txt"));

                List<UnlockableSuit> suits = UnityEngine.Object.FindObjectsOfType<UnlockableSuit>().ToList<UnlockableSuit>();
                suits = suits.OrderBy(suit => suit.syncedSuitID.Value).ToList();
                int index = 0;
                foreach (UnlockableSuit suit in suits.Where(suit => mySuits.Contains(suit.name)))
                {
                    AutoParentToShip component = suit.gameObject.GetComponent<AutoParentToShip>();
                    component.overrideOffset = true;

                    float offsetModifier = 0.18f;
                    if (MakeSuitsFitOnRack && suits.Count > 13)
                    {
                        offsetModifier = offsetModifier / (Math.Min(suits.Count, 20) / 12f); // squish the suits together to make them all fit
                    }

                    component.positionOffset = new Vector3(-2.45f, 2.75f, -8.41f) + __instance.rightmostSuitPosition.forward * offsetModifier * (float)index;
                    component.rotationOffset = new Vector3(0f, 90f, 0f);

                    index++;
                }

                return false; // don't run the original
            }
        }

        private static TerminalNode cancelPurchase;
        private static TerminalKeyword buyKeyword;
        private static UnlockableItem AddToRotatingShop(UnlockableItem newSuit, int price, int unlockableID)
        {
            Terminal terminal = FindObjectOfType<Terminal>();
            for (int i = 0; i < terminal.terminalNodes.allKeywords.Length; i++)
            {
                if (terminal.terminalNodes.allKeywords[i].name == "Buy")
                {
                    buyKeyword = terminal.terminalNodes.allKeywords[i];
                    break;
                }
            }

            newSuit.alreadyUnlocked = false;
            newSuit.hasBeenMoved = false;
            newSuit.placedPosition = Vector3.zero;
            newSuit.placedRotation = Vector3.zero;

            newSuit.shopSelectionNode = ScriptableObject.CreateInstance<TerminalNode>();
            newSuit.shopSelectionNode.name = newSuit.unlockableName + "SuitBuy1";
            newSuit.shopSelectionNode.creatureName = newSuit.unlockableName + " suit";
            newSuit.shopSelectionNode.displayText = "You have requested to order " + newSuit.unlockableName + " suits.\nTotal cost of item: [totalCost].\n\nPlease CONFIRM or DENY.\n\n";
            newSuit.shopSelectionNode.clearPreviousText = true;
            newSuit.shopSelectionNode.shipUnlockableID = unlockableID;
            newSuit.shopSelectionNode.itemCost = price;
            newSuit.shopSelectionNode.overrideOptions = true;

            CompatibleNoun confirm = new CompatibleNoun();
            confirm.noun = ScriptableObject.CreateInstance<TerminalKeyword>();
            confirm.noun.word = "confirm";
            confirm.noun.isVerb = true;

            confirm.result = ScriptableObject.CreateInstance<TerminalNode>();
            confirm.result.name = newSuit.unlockableName + "SuitBuyConfirm";
            confirm.result.creatureName = "";
            confirm.result.displayText = "Ordered " + newSuit.unlockableName + " suits! Your new balance is [playerCredits].\n\n";
            confirm.result.clearPreviousText = true;
            confirm.result.shipUnlockableID = unlockableID;
            confirm.result.buyUnlockable = true;
            confirm.result.itemCost = price;
            confirm.result.terminalEvent = "";

            CompatibleNoun deny = new CompatibleNoun();
            deny.noun = ScriptableObject.CreateInstance<TerminalKeyword>();
            deny.noun.word = "deny";
            deny.noun.isVerb = true;

            if (cancelPurchase == null)
            {
                cancelPurchase = ScriptableObject.CreateInstance<TerminalNode>(); // we can use the same Cancel Purchase node
            }
            deny.result = cancelPurchase;
            deny.result.name = "MoreSuitsCancelPurchase";
            deny.result.displayText = "Cancelled order.\n";

            newSuit.shopSelectionNode.terminalOptions = new CompatibleNoun[] { confirm, deny };

            TerminalKeyword suitKeyword = ScriptableObject.CreateInstance<TerminalKeyword>();
            suitKeyword.name = newSuit.unlockableName + "Suit";
            suitKeyword.word = newSuit.unlockableName.ToLower() + " suit";
            suitKeyword.defaultVerb = buyKeyword;

            CompatibleNoun suitCompatibleNoun = new CompatibleNoun();
            suitCompatibleNoun.noun = suitKeyword;
            suitCompatibleNoun.result = newSuit.shopSelectionNode;
            List<CompatibleNoun> buyKeywordList = buyKeyword.compatibleNouns.ToList<CompatibleNoun>();
            buyKeywordList.Add(suitCompatibleNoun);
            buyKeyword.compatibleNouns = buyKeywordList.ToArray();

            List<TerminalKeyword> allKeywordsList = terminal.terminalNodes.allKeywords.ToList();
            allKeywordsList.Add(suitKeyword);
            allKeywordsList.Add(confirm.noun);
            allKeywordsList.Add(deny.noun);
            terminal.terminalNodes.allKeywords = allKeywordsList.ToArray();

            return newSuit;
        }

        public static bool TryParseVector4(string input, out Vector4 vector)
        {
            vector = Vector4.zero;

            string[] components = input.Split(',');

            if (components.Length == 4)
            {
                if (float.TryParse(components[0], out float x) &&
                    float.TryParse(components[1], out float y) &&
                    float.TryParse(components[2], out float z) &&
                    float.TryParse(components[3], out float w))
                {
                    vector = new Vector4(x, y, z, w);
                    return true;
                }
            }

            return false;
        }
    }
}