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

        /*
         * ---------- REQUIREMENTS ----------
         * The script is used by giving the command as an argument for the sending ProgrammableBlock.
         * The timer you want to activate can have a name of your choosing, but the command syntax is important.
         * You need to have at least one antenna on the sending and the receiving grid.
         * You need to have the same channel on both the sender and the receiver.
         * 
         * ---------- USAGE ----------
         * To use from cockpit etc.
         * Edit toolbar and find the sending PB and set it as run and write your argument there.
         * 
         * ---------- ARGUMENTS ----------
         * Argument: Timername;Trigger/Start;GridID as the argument for the sending ProgrammableBlock
         * Where GridID is the receiving grid and Timername is the name of the timer(s) you want to activate.
         * 
         * ---------- EXAMPLES ----------
         * Example 1: Timer Hangar;Trigger;Base
         * Example 2: Timer Hangar;Start;Base
         * 
         * ---------- SETTINGS ----------
         * GridID can be changed by using #new as the argument.
         * Channel can be changed by using %new as the argument.
         * Default destination can be changed by using &new as the argument.
         * Where "new" is your new GridID/Channel/Default destination
        */

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

        #endregion


        public string channel = "Channel 1";
        public string myGridID = "default";
        public string previous_command = "";
        public IMyBroadcastListener myBroadcastListener;

        IMyTextSurface lcd;

        public Program()
        {
            Echo("Running setup.");

            // Load setting from Storage
            if (Storage.Contains(";"))
            {
                string[] words = Storage.Split(';');
                if (words.Length == 3)
                {
                    myGridID = words[0];
                    channel = words[1];
                    default_destination = words[2];
                }
            }
            else if (Me.CustomData.Contains(";")) // Load settings from Custom Data for backwards compatibility
            {
                string[] words = Me.CustomData.Split(';');
                if (words.Length == 3)
                {
                    myGridID = words[0];
                    channel = words[1];
                    default_destination = words[2];
                }
            }
            else
            {
                Storage = myGridID + ";" + channel + ";" + default_destination;
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
            string[] words = Storage.Split(';');
            Storage = myGridID + ";" + channel + ";" + default_destination;
        }

        public void Main(string arg)
        {

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

            #region Send command
            string messageOut = arg;
            if (messageOut.Length > 5)
            {
                // Append default destination to command if set and not present in input
                if (default_destination != "" && !arg.Contains(default_destination))
                {
                    if (arg.Split(';').Length == 3) { if (arg.Split(';')[2] == "") messageOut = arg + default_destination; }
                    else if (arg.Split(';').Length == 2) messageOut = arg + ";" + default_destination;

                }
                IGC.SendBroadcastMessage(channel, messageOut, TransmissionDistance.TransmissionDistanceMax);
            }
            #endregion

            if (myBroadcastListener.HasPendingMessage)
            {
                MyIGCMessage message = myBroadcastListener.AcceptMessage();

                string messagetext = message.Data.ToString();
                //string messagetag = message.Tag;
                //long sender = message.Source;

                //Check if the incoming message is a error message and display it.
                if (messagetext.Contains(";ERRORMESSAGE"))
                {
                    Print(messagetext.Replace(";ERRORMESSAGE", ""));
                }
                //Check if incoming message is for me.
                else if (messagetext.Contains(";" + myGridID))
                {
                    previous_command = messagetext;
                    string[] timer_action = messagetext.Split(';');
                    if (timer_action.Length == 3)
                    {
                        // Get timer block(s) to activate
                        List<IMyTimerBlock> timers = new List<IMyTimerBlock>();
                        GridTerminalSystem.GetBlocksOfType<IMyTimerBlock>(timers);
                        foreach (var t in timers)
                        {
                            if (t.CustomName != timer_action[0])
                            {
                                timers.Remove(t);
                            }
                        }
                       
                        string action = timer_action[1].ToLower();

                        if (timers.Count == 0)
                        {
                            string timerNotFound = "Timer not found!";
                            Print(timerNotFound);
                            IGC.SendBroadcastMessage(channel, timerNotFound + ";ERRORMESSAGE", TransmissionDistance.TransmissionDistanceMax);
                            return;
                        }
                        else if (action == "start" || action == "trigger")
                        {
                            if (action == "trigger") timer_action[1] = "TriggerNow";
                            else if (action == "start") timer_action[1] = "Start";
                            foreach (IMyTimerBlock timer in timers) timer.ApplyAction(timer_action[1]);
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
            }

            Print(); // Print to display and echo

            Print("Grid ID : " + myGridID);
            Print("Channel: " + channel);

            if (default_destination != "") Print("Default Destination: " + default_destination);

            Print("Sent message: " + messageOut);

            if (show_previous == true && previous_command != "")
                Print("Previously received command:" + "\n" + previous_command.Split(';')[0] + "\n" + previous_command.Split(';')[1] + "\n" + previous_command.Split(';')[2]);

            Print("\nCommands:\nGridID: #\nChannel: %\nDefault destination: &");
            /*
            GridID can be changed by using #new as the argument.
            Channel can be changed by using %new as the argument.
            Default destination can be changed by using &new as the argument.
            */

        }

    }
}
