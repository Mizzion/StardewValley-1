﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Objects;
using Object = StardewValley.Object;
using SFarmer = StardewValley.Farmer;

namespace ExtremePetting
{
    public class AnimalSitter : Mod
    {
        /*********
        ** Properties
        *********/
        private Keys PetKey;

        // Whether to use dark magic to age the animals to maturity when visiting the animals.
        private bool GrowUpEnabled = true;

        // Whether to pet the animal until their maximum happiness level is reached.
        private bool MaxHappinessEnabled = true;

        // Whether to feed the animals to their max fullness when visiting.
        private bool MaxFullnessEnabled = true;

        // Whether to harvest animal drops while visiting.
        private bool HarvestEnabled = true;

        // Whether to pet animals as they are visited.
        private bool PettingEnabled = true;

        // Whether to max the animal's friendship toward the farmer while visiting, even though the farmer is completely ignoring them.
        private bool MaxFriendshipEnabled = true;

        // Whether to display the in game dialogue messages.
        private bool MessagesEnabled = true;

        // Who does the checking.
        private string Checker = "spouse";

        // How much to charge per animal.
        private int CostPerAnimal;

        // Whether to snatch hidden truffles from the snout of the pig.
        private bool TakeTrufflesFromPigs = true;

        // Coordinates of the default chest.
        private Vector2 ChestCoords = new Vector2(73f, 14f);

        // Whether to bypass the inventory, and first attempt to deposit the harvest into the chest.  Inventory is then used as fallback.
        private bool BypassInventory;

        // A string defining the locations of specific chests.
        private String ChestDefs = "";

        // Whether both inventory and chests are full.
        private bool InventoryAndChestFull;

        // How many days the farmer has not been able to afford to pay the laborer.
        private int ShortDays;

        private AnimalSitterConfig Config;

        private DialogueManager DialogueManager;

        private ChestManager ChestManager;


        /*********
        ** Public methods
        *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            this.Config = this.Helper.ReadConfig<AnimalSitterConfig>();
            this.DialogueManager = new DialogueManager(this.Config, helper.Content, this.Monitor);
            this.ChestManager = new ChestManager(this.Monitor);

            SaveEvents.AfterLoad += this.SaveEvents_AfterLoad;
            ControlEvents.KeyReleased += this.ControlEvents_KeyReleased;
        }


        private void SaveEvents_AfterLoad(object sender, EventArgs e)
        {
            this.ImportConfiguration();

            //parseChestLocations();
            this.ChestManager.ParseChests(this.ChestDefs);
            this.ChestManager.SetDefault(this.ChestCoords);

            // Read in dialogue
            this.DialogueManager.ReadInMessages();

            this.Monitor.Log($"chestCoords:{this.ChestCoords.X},{this.ChestCoords.Y}", LogLevel.Trace);
        }

        private void ImportConfiguration()
        {
            if (!Enum.TryParse(this.Config.KeyBind, true, out this.PetKey))
            {
                this.PetKey = Keys.O;
                this.Monitor.Log("Error parsing key binding. Defaulted to O");
            }

            this.PettingEnabled = this.Config.PettingEnabled;
            this.GrowUpEnabled = this.Config.GrowUpEnabled;
            this.MaxHappinessEnabled = this.Config.MaxHappinessEnabled;
            this.MaxFriendshipEnabled = this.Config.MaxFriendshipEnabled;
            this.MaxFullnessEnabled = this.Config.MaxFullnessEnabled;
            this.HarvestEnabled = this.Config.HarvestEnabled;
            this.Checker = this.Config.WhoChecks;
            this.MessagesEnabled = this.Config.EnableMessages;
            this.TakeTrufflesFromPigs = this.Config.TakeTrufflesFromPigs;
            this.ChestCoords = this.Config.ChestCoords;

            this.BypassInventory = this.Config.BypassInventory;
            this.ChestDefs = this.Config.ChestDefs;

            if (this.Config.CostPerAction < 0)
            {
                this.Monitor.Log("I'll do it for free, but I'm not paying YOU to take care of YOUR stinking animals!", LogLevel.Trace);
                this.Monitor.Log("Setting costPerAction to 0.", LogLevel.Trace);
                this.CostPerAnimal = 0;
            }
            else
            {
                this.CostPerAnimal = this.Config.CostPerAction;
            }
        }

        private void ControlEvents_KeyReleased(object sender, EventArgsKeyPressed e)
        {
            if (Game1.currentLocation == null
                || (Game1.player == null
                || Game1.hasLoadedGame == false)
                || ((Game1.player).UsingTool
                || !(Game1.player).CanMove
                || (Game1.activeClickableMenu != null
                || Game1.CurrentEvent != null))
                || Game1.gameMode != 3)
            {

                return;
            }

            if (e.KeyPressed == this.PetKey)
            {
                try
                {
                    this.IterateOverAnimals();
                }
                catch (Exception ex)
                {
                    this.Monitor.Log($"Exception onKeyReleased: {ex}", LogLevel.Error);
                }
            }
        }

        private void IterateOverAnimals()
        {
            SFarmer farmer = Game1.player;
            Farm farm = Game1.getFarm();
            AnimalTasks stats = new AnimalTasks();

            foreach (FarmAnimal animal in this.GetAnimals())
            {
                try
                {
                    if (!animal.wasPet && this.PettingEnabled)
                    {
                        animal.pet(Game1.player);
                        stats.AnimalsPet++;

                        this.Monitor.Log($"Petting animal: {animal.name}", LogLevel.Trace);
                    }


                    if (this.GrowUpEnabled && animal.isBaby())
                    {
                        this.Monitor.Log($"Aging animal to mature+1 days: {animal.name}", LogLevel.Trace);

                        animal.age = animal.ageWhenMature + 1;
                        animal.reload();
                        stats.Aged++;
                    }

                    if (this.MaxFullnessEnabled && animal.fullness < byte.MaxValue)
                    {
                        this.Monitor.Log($"Feeding animal: {animal.name}", LogLevel.Trace);

                        animal.fullness = byte.MaxValue;
                        stats.Fed++;
                    }

                    if (this.MaxHappinessEnabled && animal.happiness < byte.MaxValue)
                    {
                        this.Monitor.Log($"Maxing Happiness of animal {animal.name}", LogLevel.Trace);

                        animal.happiness = byte.MaxValue;
                        stats.MaxHappiness++;
                    }

                    if (this.MaxFriendshipEnabled && animal.friendshipTowardFarmer < 1000)
                    {
                        this.Monitor.Log($"Maxing Friendship of animal {animal.name}", LogLevel.Trace);

                        animal.friendshipTowardFarmer = 1000;
                        stats.MaxFriendship++;
                    }

                    if (animal.currentProduce > 0 && this.HarvestEnabled)
                    {
                        this.Monitor.Log($"Has produce: {animal.name} {animal.currentProduce}", LogLevel.Trace);

                        if (animal.type.Equals("Pig"))
                        {
                            if (this.TakeTrufflesFromPigs)
                            {
                                //Game1.player.addItemToInventoryBool((Item)new StardewValley.Object(animal.currentProduce, 1, false, -1, animal.produceQuality), false);
                                Object toAdd = new Object(animal.currentProduce, 1, false, -1, animal.produceQuality);
                                this.AddItemToInventory(toAdd, farmer, farm, stats);

                                animal.currentProduce = 0;
                                stats.TrufflesHarvested++;
                            }
                        }
                        else
                        {
                            Object toAdd = new Object(animal.currentProduce, 1, false, -1, animal.produceQuality);
                            this.AddItemToInventory(toAdd, farmer, farm, stats);

                            animal.currentProduce = 0;
                            stats.ProductsHarvested++;
                        }


                    }
                }
                catch (Exception ex)
                {
                    this.Monitor.Log($"Exception onKeyReleased: {ex}", LogLevel.Error);
                }
            }

            this.HarvestTruffles(stats);
            this.HarvestCoops(stats);

            int actions = stats.GetTaskCount();
            bool gatheringOnly = stats.JustGathering();

            if (actions > 0 && this.CostPerAnimal > 0)
            {
                int totalCost = actions * this.CostPerAnimal;
                bool doesPlayerHaveEnoughCash = Game1.player.Money >= totalCost;
                Game1.player.Money = Math.Max(0, Game1.player.Money - totalCost);

                if (this.MessagesEnabled)
                    this.ShowMessage(actions, totalCost, doesPlayerHaveEnoughCash, gatheringOnly, stats);

                this.Monitor.Log($"Animal sitter performed {actions} actions. Total cost: {totalCost}g", LogLevel.Trace);

            }
            else if (actions == 0 && this.CostPerAnimal > 0)
            {
                if (this.MessagesEnabled)
                {
                    HUDMessage msg = new HUDMessage("There's nothing to do for the animals right now.");
                    Game1.addHUDMessage(msg);
                }

                this.Monitor.Log("There's nothing to do for the animals right now.", LogLevel.Trace);
            }
        }
        
        private void HarvestTruffles(AnimalTasks stats)
        {
            Farm farm = Game1.getFarm();
            SFarmer farmer = Game1.player;

            List<Vector2> itemsToRemove = new List<Vector2>();

            // Iterate over the objects, and add them to inventory.
            foreach (KeyValuePair<Vector2, Object> keyvalue in farm.Objects)
            {
                Object obj = keyvalue.Value;

                if (obj.Name == "Truffle")
                {
                    bool doubleHarvest = false;

                    if (Game1.player.professions.Contains(16))
                        obj.quality = 4;

                    double randomNum = Game1.random.NextDouble();
                    bool doubleChance = (this.Checker.Equals("pet")) ? (randomNum < 0.4) : (randomNum < 0.2);

                    if (Game1.player.professions.Contains(13) && doubleChance)
                    {
                        obj.Stack = 2;
                        doubleHarvest = true;
                    }

                    if (this.AddItemToInventory(obj, farmer, farm, stats))
                    {
                        itemsToRemove.Add(keyvalue.Key);
                        farmer.gainExperience(2, 7);
                        stats.TrufflesHarvested++;

                        if (doubleHarvest)
                        {
                            stats.TrufflesHarvested++;
                            farmer.gainExperience(2, 7);
                        }

                    }
                    else
                    {
                        this.Monitor.Log("Inventory full, could not add animal product.", LogLevel.Trace);
                    }
                }

            }

            // Now remove the items
            foreach (Vector2 itemLocation in itemsToRemove)
            {
                farm.removeObject(itemLocation, false);
            }

        }

        private void HarvestCoops(AnimalTasks stats)
        {
            Farm farm = Game1.getFarm();
            SFarmer farmer = Game1.player;

            foreach (Building building in farm.buildings)
            {
                if (building is Coop)
                {
                    List<Vector2> itemsToRemove = new List<Vector2>();

                    foreach (KeyValuePair<Vector2, Object> keyvalue in building.indoors.Objects)
                    {
                        Object obj = keyvalue.Value;

                        this.Monitor.Log($"Found coop object: {obj.Name} / {obj.Category}/{obj.isAnimalProduct()}", LogLevel.Trace);

                        if (obj.isAnimalProduct() || obj.parentSheetIndex == 107)
                        {
                            if (this.AddItemToInventory(obj, farmer, farm, stats))
                            {
                                itemsToRemove.Add(keyvalue.Key);
                                stats.ProductsHarvested++;
                                farmer.gainExperience(0, 5);
                            }
                            else
                            {
                                this.Monitor.Log("Inventory full, could not add animal product.", LogLevel.Trace);
                            }
                        }
                    }

                    // Remove the object that were picked up.
                    foreach (Vector2 itemLocation in itemsToRemove)
                    {
                        building.indoors.removeObject(itemLocation, false);
                    }
                }
            }
        }

        private bool AddItemToInventory(Object obj, SFarmer farmer, Farm farm, AnimalTasks stats)
        {
            bool wasAdded = false;

            if (!this.BypassInventory)
            {
                if (farmer.couldInventoryAcceptThisItem(obj))
                {
                    farmer.addItemToInventory(obj);
                    return true;
                }
            }

            // Get the preferred chest (could be default)
            Object chest = this.ChestManager.GetChest(obj.parentSheetIndex);

            if (chest != null && (chest is Chest))
            {
                Item i = ((Chest)chest).addItem(obj);
                if (i == null)
                    return true;
            }

            // We haven't returned, get the default chest.
            chest = this.ChestManager.GetDefaultChest();

            if (chest != null && (chest is Chest))
            {
                Item i = ((Chest)chest).addItem(obj);
                if (i == null)
                    return true;
            }

            // Haven't been able to add to a chest, try inventory one last time.
            if (farmer.couldInventoryAcceptThisItem(obj))
            {
                farmer.addItemToInventory(obj);
                return true;
            }

            this.InventoryAndChestFull = true;
            return wasAdded;
        }


        private String GetGathererName()
        {
            if (this.Checker.ToLower() == "spouse")
            {
                if (Game1.player.isMarried())
                    return Game1.player.getSpouse().getName();
                else
                    return "The animal sitter";
            }
            else
            {
                return this.Checker;
            }

        }


        private void ShowMessage(int numActions, int totalCost, bool doesPlayerHaveEnoughCash, bool gatheringOnly, AnimalTasks stats)
        {
            stats.NumActions = numActions;
            stats.TotalCost = totalCost;

            string message = "";

            if (this.Checker.ToLower() == "pet")
            {
                if (Game1.player.hasPet())
                {
                    if (Game1.player.catPerson)
                    {
                        message += "Meow..";
                    }
                    else
                    {
                        message += "Woof.";
                    }
                }
                else
                {
                    message += "Your imaginary pet has taken care of your animals.";
                }

                HUDMessage msg = new HUDMessage(message);
                Game1.addHUDMessage(msg);
            }
            else
            {
                if (this.Checker.ToLower() == "spouse")
                {
                    if (Game1.player.isMarried())
                    {
                        message += DialogueManager.PerformReplacement(DialogueManager.GetMessageAt(1, "Xdialog"), stats, this.Config);
                    }
                    else
                    {
                        message += DialogueManager.PerformReplacement(DialogueManager.GetMessageAt(2, "Xdialog"), stats, this.Config);
                    }

                    if (totalCost > 0 && this.CostPerAnimal > 0)
                    {
                        message += DialogueManager.PerformReplacement(DialogueManager.GetMessageAt(3, "Xdialog"), stats, this.Config);
                    }

                    HUDMessage msg = new HUDMessage(message);
                    Game1.addHUDMessage(msg);
                }
                else if (gatheringOnly)
                {
                    message += DialogueManager.PerformReplacement(DialogueManager.GetMessageAt(4, "Xdialog"), stats, this.Config);

                    if (totalCost > 0 && this.CostPerAnimal > 0)
                    {
                        message += DialogueManager.PerformReplacement(DialogueManager.GetMessageAt(3, "Xdialog"), stats, this.Config);
                    }

                    HUDMessage msg = new HUDMessage(message);
                    Game1.addHUDMessage(msg);
                }
                else
                {
                    NPC character = Game1.getCharacterFromName(this.Checker);
                    if (character != null)
                    {
                        //this.isCheckerCharacter = true;
                        string portrait = "";
                        if (character.name.Equals("Shane"))
                        {
                            portrait = "$8";
                        }

                        string spouseName = null;
                        if (Game1.player.isMarried())
                        {
                            spouseName = Game1.player.getSpouse().getName();
                        }

                        message += DialogueManager.PerformReplacement(DialogueManager.GetRandomMessage("greeting"), stats, this.Config);
                        message += DialogueManager.PerformReplacement(DialogueManager.GetMessageAt(5, "Xdialog"), stats, this.Config);

                        if (this.CostPerAnimal > 0)
                        {
                            if (doesPlayerHaveEnoughCash)
                            {
                                message += DialogueManager.PerformReplacement(DialogueManager.GetMessageAt(6, "Xdialog"), stats, this.Config);
                                this.ShortDays = 0;
                            }
                            else
                            {
                                message += DialogueManager.PerformReplacement(DialogueManager.GetRandomMessage("unfinishedmoney"), stats, this.Config);
                            }
                        }
                        else
                        {

                            //message += portrait + "#$e#";
                        }

                        message += DialogueManager.PerformReplacement(DialogueManager.GetRandomMessage("smalltalk"), stats, this.Config);
                        message += portrait + "#$e#";

                        character.CurrentDialogue.Push(new Dialogue(message, character));
                        Game1.drawDialogue(character);
                    }
                    else
                    {
                        //message += checker + " has performed " + numActions + " for your animals.";
                        message += DialogueManager.PerformReplacement(DialogueManager.GetMessageAt(7, "Xdialog"), stats, this.Config);
                        HUDMessage msg = new HUDMessage(message);
                        Game1.addHUDMessage(msg);
                    }
                }
            }

        }

        private List<FarmAnimal> GetAnimals()
        {
            List<FarmAnimal> list = Game1.getFarm().animals.Values.ToList();
            foreach (Building building in Game1.getFarm().buildings)
            {
                if (building.indoors != null && building.indoors.GetType() == typeof(AnimalHouse))
                    list.AddRange(((AnimalHouse)building.indoors).animals.Values.ToList());
            }
            return list;
        }

    }
}
