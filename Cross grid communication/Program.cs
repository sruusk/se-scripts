using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {

        #region mdk preserve

        // Change this to 1, if you want the info to be displayed on the small screen.
        static int lcd_id = 0;

        // Change this to false, if you don't want to display info on screens.
        static bool print_on_lcd = true;

        // Set your default destination Grid ID here, if you're only going to be sending to the same grid.
        // If this is set it isn't required to write the Grid ID on the argument when sending.
        private string default_destination = "";

        // Shows the previously received command on screen.
        // This is mainly for debugging.
        static bool show_previous = false;



        // ------------------ Script Start ------------------ //
        string channel = "Channel 1", myGridID = "default";

        #endregion

        // Broadcast listener to receive messages on.
        IMyBroadcastListener myBroadcastListener;
        // LCD to display info on.
        IMyTextSurface lcd;

        // How many loops to skip.
        int wait = 0;

        int counter = 0;
        string[] running = new string[] { "|", "/", "-", "\\" };

        public Program()
        {
            Echo("Running setup.");

            // Load settings from Storage or CustomData for backwards compatibility
            if (Storage.Contains(";") || Me.CustomData.Contains(";")) 
            {
                string[] words = Storage.Contains(";") ? Storage.Split(';') : Me.CustomData.Split(';');
                if (words.Length == 3)
                {
                    myGridID = words[0];
                    channel = words[1];
                    default_destination = words[2];
                }
            }

            // Init lcd
            if (print_on_lcd)
            {
                lcd = Me.GetSurface(lcd_id);
                if (lcd.ContentType != ContentType.TEXT_AND_IMAGE) lcd.ContentType = ContentType.TEXT_AND_IMAGE;
                if (lcd_id == 1) lcd.FontSize = 2.5F;
                else lcd.FontSize = 1.2F;
                lcd.FontColor = Color.Gray;
                lcd.Alignment = TextAlignment.LEFT;
                lcd.WriteText("Running Setup", false);
            }

            // Add broadcast listener
            myBroadcastListener = IGC.RegisterBroadcastListener(channel);
            //List<IMyBroadcastListener> listeners = new List<IMyBroadcastListener>();
            //IGC.GetBroadcastListeners(listeners);

            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public string logText = "";
        public void Print(string new_text = "") // Helper function for printing text to lcd's
        {
            // Displays text on detailed info and lcd.
            // Every new string is put on a new row.
            // input: string
            // If input is empty or an empty string: display text and reset.

            if (new_text == "" && logText != "")
            {
                Echo(logText);
                if (print_on_lcd && lcd != null)
                {
                    lcd.WriteText(logText, false);
                }
                logText = "";
            }
            else
            {
                logText += new_text;
                logText += "\n";
            }
        }

        public void Save()
        {
            Storage = string.Join(";", new string[] { myGridID, channel, default_destination });
        }

        public void Main(string arg)
        {
            if (wait > 0) { wait--; return; }

            #region Settings
            if (arg.Contains("#"))
            {
                string newID = arg.Replace("#", "");
                myGridID = newID;
                Save();
                Print("ID set to " + newID);
                Print();
                return;
            }
            else if (arg.Contains("%"))
            {
                if (arg.Length > 5)
                {
                    IGC.DisableBroadcastListener(myBroadcastListener);
                    channel = arg.Replace("%", "");
                    myBroadcastListener = IGC.RegisterBroadcastListener(channel);
                    Save();
                    Print("Channel set to " + channel);
                }
                else
                {
                    Print("Channel too short.\nMust be atleast 5 characters");
                    if (print_on_lcd) lcd.WriteText("Channel too short.\nMust be atleast 5 characters", false);
                }
                Print();
                return;
            }
            else if (arg.Contains("&"))
            {
                default_destination = arg.Replace("&", "");
                Save();
                Print("Default destination set to " + default_destination);
                Print();
                return;
            }
            #endregion

            #region Send broadcast
            string[] messageOut = arg.Contains(';') ? arg.Split(';') : new string[] { };
            if (messageOut.Length == 3 || messageOut.Length == 2)
            {
                // Append default destination to command if set and not present in input
                if (default_destination != "")
                {
                    if(messageOut.Length == 2) messageOut.Append(default_destination);
                    else if (messageOut[2] == "") messageOut[2] = default_destination;
                }
                else if (messageOut.Length == 2 || messageOut[2] == "")
                {
                    Print("Invalid argument");
                    Print();
                    return;
                }
                IGC.SendBroadcastMessage(channel, messageOut, TransmissionDistance.TransmissionDistanceMax);
            }
            #endregion

            #region Receive broadcast
            string receivedCommand = "";
            if (myBroadcastListener.HasPendingMessage)
            {
                MyIGCMessage message = myBroadcastListener.AcceptMessage();

                string messagetext = message.Data.ToString();
                //string messagetag = message.Tag;
                //long sender = message.Source;

                string[] messageParts = messagetext.Split(';');

                //Check if the incoming message is an error message and display it.
                if (messageParts.Length == 2 && messageParts[1] == "ERRORMESSAGE")
                {
                    Print("Error: " + messageParts[0]);
                    wait = 3;
                }
                //Check if incoming message is for me.
                else if (messageParts.Length == 3 && messageParts[2] == myGridID)
                {
                    receivedCommand = messagetext;

                    // Get timer block(s) to activate
                    List<IMyTimerBlock> timers = new List<IMyTimerBlock>();
                    GridTerminalSystem.GetBlocksOfType<IMyTimerBlock>(timers);
                    timers = timers.FindAll(x => x.CustomName.ToLower() == messageParts[0].ToLower());
                       
                    string action = messageParts[1].ToLower();

                    if (timers.Count == 0)
                    {
                        string timerNotFound = "Timer not found!";
                        Print(timerNotFound);
                        IGC.SendBroadcastMessage(channel, timerNotFound + ";ERRORMESSAGE", TransmissionDistance.TransmissionDistanceMax);
                        return;
                    }
                    else if (action == "start" || action == "trigger")
                    {
                        if (action == "trigger") action = "TriggerNow";
                        else if (action == "start") action = "Start";
                        foreach (IMyTimerBlock timer in timers) timer.ApplyAction(action);
                    }
                    else
                    {
                        Print("Invalid command received!");
                        IGC.SendBroadcastMessage(
                            channel,
                            "Invalid command!\nCheck that you have used the correct syntax.\nCheck that you have written Start or Trigger properly.;ERRORMESSAGE",
                            TransmissionDistance.TransmissionDistanceMax
                        );
                    }
                }
            }
            #endregion


            if(messageOut.Length > 1) Print("Sent message: " + string.Join(";", messageOut));

            if (show_previous && receivedCommand != "")
                Print("Previously received command:\n" + string.Join("\n", receivedCommand.Split(';')));

            Print("\nCommands:\n - GridID: #\n - Channel: %\n - Default destination: &");
            /*
            GridID can be changed by using #new as the argument.
            Channel can be changed by using %new as the argument.
            Default destination can be changed by using &new as the argument.
            */

            Print();

            // Print running indicator
            Print("Running: " + running[counter]);
            counter++;
            if (counter >= running.Length) counter = 0;

            Print("Grid ID: " + myGridID);
            Print("Channel: " + channel);

            if (default_destination != "") Print("Default Destination: " + default_destination);
        }

    }
}
