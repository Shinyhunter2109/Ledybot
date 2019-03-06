using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using System.Windows.Forms;

namespace Ledybot
{
    class GTSBot6
    {

        //private System.IO.StreamWriter file = new StreamWriter(@"C:\Temp\ledylog.txt");

        public enum gtsbotstates { botstart, startsearch, pressSeek, presssearch, findPokemon, trade, research, botexit, updatecomments, panic };

        private TcpClient client = new TcpClient();
        private string consoleName = "Ledybot";
        private IPEndPoint serverEndPoint = null;
        private bool useLedySync = false;

        private const int SEARCHDIRECTION_FROMBACK = 0;
        private const int SEARCHDIRECTION_FROMBACKFIRSTPAGEONLY = 1;
        private const int SEARCHDIRECTION_FROMFRONT = 2;

        private bool correctScreen;

        uint PSSMenuOFF;
        uint PSSMenuIN;
        uint PSSMenuOUT;

        uint IsConnected;

        uint currentScreen;

        int SeekDepositScreen;
        int SearchScreen;
        int GTSScreen;

        uint GTSPageSize;
        uint GTSPageIndex;
        uint GTSCurrentView;

        uint GTSListBlock;

        uint GTSBlockEntrySize;

        uint BoxInject;

        uint PokemonToFind;
        uint PokemonToFindGender;
        uint PokemonToFindLevel;

        private int iPokemonToFind = 0;
        private int iPokemonToFindGender = 0;
        private int iPokemonToFindLevel = 0;
        private int iPID = 0;
        private string szIP;
        private bool bBlacklist = false;
        private bool bReddit = false;
        private int searchDirection = 0;
        private string szFC = "";
        private byte[] principal = new byte[4];

        public bool botstop = false;

        public bool PokemonFound { get; private set; }
        public uint CurrentView { get; private set; }
        public uint PageIndex { get; private set; }

        private int botState = 0;
        public int botresult = 0;
        Task<bool> waitTaskbool;
        private int commandtime = 250;
        private int delaytime = 150;
        private int o3dswaittime = 1000;

        private int startIndex = 100;
        private byte[] blockbytes = new byte[256];
        private byte[] block = new byte[256];

        private bool foundLastPage = false;

        private Tuple<string, string, int, int, int, ArrayList> details;
        private short dex;

        private int iStartIndex;
        private int iEndIndex;
        private int iDirection;

        public string szTrainerName { get; private set; }
        public string Phrase { get; private set; }
        public string Message { get; private set; }

        private async Task<bool> isCorrectWindow(int expectedScreen)
        {
            await Task.Delay(o3dswaittime);
            await Program.helper.waitNTRread(currentScreen);
            int screenID = (int)Program.helper.lastRead;

            //file.WriteLine("Checkscreen: " + expectedScreen + " - " + screenID + " botstate:" + botState);
            //file.Flush();
            return expectedScreen == screenID;
        }

        private Boolean canThisTrade(byte[] principal, string consoleName, string trainerName, string country, string region, string pokemon, string szFC, string page, string index)
        {
            NetworkStream clientStream = client.GetStream();
            byte[] buffer = new byte[4096];
            byte[] messageID = { 0x00 };
            string szmessage = consoleName + '\t' + trainerName + '\t' + country + '\t' + region + '\t' + pokemon + '\t' + page + "\t" + index + "\t";
            byte[] toSend = Encoding.UTF8.GetBytes(szmessage);

            buffer = messageID.Concat(principal).Concat(toSend).ToArray();
            clientStream.Write(buffer, 0, buffer.Length);
            clientStream.Flush();
            byte[] message = new byte[4096];
            try
            {
                //blocks until a client sends a message
                int bytesRead = clientStream.Read(message, 0, 4096);
                if (message[0] == 0x02)
                {
                    Program.f1.banlist.Add(szFC);
                }
                return message[0] == 0x01;
            }
            catch
            {
                return false;
                //a socket error has occured
            }
        }

        public GTSBot6(int iP, int iPtF, int iPtFGender, int iPtFLevel, bool bBlacklist, bool bReddit, int iSearchDirection, string waittime, string consoleName, bool useLedySync, string ledySyncIp, string ledySyncPort, int game, string szIP)
        {
            this.iPokemonToFind = iPtF;
            this.iPokemonToFindGender = iPtFGender;
            this.iPokemonToFindLevel = iPtFLevel;
            this.iPID = iP;
            this.szIP = szIP;
            this.bBlacklist = bBlacklist;
            this.bReddit = bReddit;
            this.searchDirection = iSearchDirection;
            this.o3dswaittime = Int32.Parse(waittime);
            if (useLedySync)
            {
                this.useLedySync = useLedySync;
                int iPort = Int32.Parse(ledySyncPort);
                this.serverEndPoint = new IPEndPoint(IPAddress.Parse(ledySyncIp), iPort);
                client.Connect(serverEndPoint);
            }
            this.consoleName = consoleName;

            if (game == 3) // Omega Rubin and Alpha Sapphire
            {
                PSSMenuOFF = 0x19C21C;
                PSSMenuIN = 0x83E0C8;
                PSSMenuOUT = 0x50DFB0;

                currentScreen = 0x08335290;

                SeekDepositScreen = 0x20F1EC;
                SearchScreen = 0x00;
                GTSScreen = 0x5DD4A0;

                IsConnected = 0x602110;

                GTSPageSize = 0x08C6D69C;
                GTSPageIndex = 0x08C6945C;
                GTSCurrentView = 0x08C6D6AC;

                GTSListBlock = 0x8C694F8;
                GTSBlockEntrySize = 0xA0;

                BoxInject = 0x8C9E134;


                PokemonToFind = 0x08335290;
                PokemonToFindLevel = 0x08335298;
                PokemonToFindGender = 0x08335294;
            }
            if (game == 4) // X and Y
            {
                PSSMenuOFF = 0x19ABC0;
                PSSMenuIN = 0x7EF0C8;
                PSSMenuOUT = 0x4D7BC0;

                currentScreen = 0x08334988;

                SeekDepositScreen = 0x1BBD08;
                SearchScreen = 0x00;
                GTSScreen = 0x8381D1C;

                IsConnected = 0x645180;

                GTSPageSize = 0x08C66080;
                GTSPageIndex = 0x08C61E40;
                GTSCurrentView = 0x08C66090;

                GTSListBlock = 0x8C61EDC;
                GTSBlockEntrySize = 0xA0;

                BoxInject = 0x8C861C8;

                PokemonToFind = 0x08334988;
                PokemonToFindLevel = 0x08334990;
                PokemonToFindGender = 0x0833498C;
            }
        }

        public async Task<int> RunBot()
        {
            byte[] pokemonIndex = new byte[2];
            byte pokemonGender = 0x0;
            byte pokemonLevel = 0x0;
            byte[] full = BitConverter.GetBytes(iPokemonToFind);
            pokemonIndex[0] = full[0];
            pokemonIndex[1] = full[1];
            full = BitConverter.GetBytes(iPokemonToFindGender);
            pokemonGender = full[0];
            full = BitConverter.GetBytes(iPokemonToFindLevel);
            pokemonLevel = full[0];

            try
            {
                while (!botstop)
                {
                    switch (botState)
                    {
                        case (int)gtsbotstates.botstart:
                            if (bReddit)
                                Program.f1.updateJSON();

                            botState = (int)gtsbotstates.pressSeek;
                            break;

                        case (int)gtsbotstates.updatecomments:
                            Program.f1.updateJSON();
                            botState = (int)gtsbotstates.research;
                            break;

                        case (int)gtsbotstates.pressSeek:

                            correctScreen = await isCorrectWindow(SeekDepositScreen);
                            if (!correctScreen)
                            {
                                botState = (int)gtsbotstates.panic;
                                break;
                            }

                            Program.f1.ChangeStatus("Pressing seek button");
                            await Program.helper.waitbutton(Program.PKTable.keyA);
                            await Task.Delay(1000);
                            botState = (int)gtsbotstates.startsearch;
                            break;

                        case (int)gtsbotstates.startsearch:

                            correctScreen = await isCorrectWindow(SearchScreen);
                            if (!correctScreen)
                            {
                                botState = (int)gtsbotstates.panic;
                                break;
                            }

                            //Write wanted Pokemon, Level, Gender to Ram, won't Display it but works.
                            Program.f1.ChangeStatus("Setting Pokemon to find");
                            waitTaskbool = Program.helper.waitNTRwrite(PokemonToFind, pokemonIndex, iPID);
                            waitTaskbool = Program.helper.waitNTRwrite(PokemonToFindGender, pokemonGender, iPID);
                            waitTaskbool = Program.helper.waitNTRwrite(PokemonToFindLevel, pokemonLevel, iPID);
                            botState = (int)gtsbotstates.presssearch;
                            break;

                        case (int)gtsbotstates.presssearch:
                            Program.f1.ChangeStatus("Pressing Search button");
                            Program.helper.quicktouch(200, 180, commandtime);

                            if (searchDirection == SEARCHDIRECTION_FROMBACK)
                            {
                                //Write Index while Loading the Frame.
                                await Task.Delay(1000);
                                await Program.helper.waitNTRwrite(GTSPageIndex, (uint)startIndex, iPID);
                            }

                            await Task.Delay(4000);
                            botState = (int)gtsbotstates.findPokemon;
                            break;

                        case (int)gtsbotstates.findPokemon:

                            correctScreen = await isCorrectWindow(GTSScreen);
                            if (!correctScreen)
                            {
                                botState = (int)gtsbotstates.panic;
                                break;
                            }
                            else
                            {
                            }

                            await Program.helper.waitNTRread(GTSPageSize);
                            uint Entries = (Program.helper.lastRead);
                            CurrentView = Entries;

                            if (Entries == 100 && !foundLastPage && searchDirection == SEARCHDIRECTION_FROMBACK)
                            {
                                Program.f1.ChangeStatus("Moving to last page");

                                int PageMoveAttemps = 0;
                                // Change current Page, everytime + 100
                                while (!foundLastPage)
                                {
                                    startIndex += 100;
                                    await Program.helper.waitNTRwrite(GTSPageIndex, (uint)startIndex, iPID);
                                    Program.helper.quickbuton(Program.PKTable.DpadLEFT, commandtime);
                                    await Task.Delay(commandtime + delaytime + 1000);
                                    Program.helper.quickbuton(Program.PKTable.DpadRIGHT, commandtime);
                                    await Task.Delay(commandtime + delaytime + 1000);
                                    await Program.helper.waitNTRread(GTSPageSize);
                                    Entries = Program.helper.lastRead;

                                    if (Entries < 99)
                                    {
                                        foundLastPage = true;
                                        CurrentView = Entries;
                                    }

                                    if (PageMoveAttemps >= 10)
                                    {
                                        // Frame writen to low, return
                                        Program.helper.quickbuton(Program.PKTable.keyB, commandtime);
                                        await Task.Delay(commandtime + delaytime + 1000);
                                        Program.helper.quickbuton(Program.PKTable.keyB, commandtime);
                                        await Task.Delay(commandtime + delaytime + 1000);
                                        botState = (int)gtsbotstates.pressSeek;
                                    }
                                    PageMoveAttemps++;
                                }
                            }

                            Program.f1.ChangeStatus("Looking for a Pokemon to Trade");

                            if (Entries > 100) { Entries = 1; } // Workaroung for only 1 Entry on List

                            // Check the Trade Direction Back to Front or Front to Back
                            if (searchDirection == SEARCHDIRECTION_FROMBACK || searchDirection == SEARCHDIRECTION_FROMBACKFIRSTPAGEONLY)
                            {
                                CurrentView = Entries;
                                iStartIndex = (int)Entries - 1;
                                iEndIndex = 1;
                                iDirection = -1;
                            }
                            else
                            {
                                CurrentView = 0;
                                iStartIndex = 1;
                                iEndIndex = (int)Entries + 1;
                                iDirection = 1;
                            }
                            // Reading all Entries on Current Page
                            await Program.helper.waitNTRread(GTSListBlock, (uint)(256 * 100));
                            byte[] blockBytes = Program.helper.lastArray;

                            for (int i = iStartIndex; i * iDirection < iEndIndex; i += iDirection)
                            {
                                //Get the Current Entry Data
                                Array.Copy(blockBytes, (GTSBlockEntrySize * i) - Program.helper.lastRead, block, 0, 256);

                                //Collect Data
                                dex = BitConverter.ToInt16(block, 0x0);

                                if (Program.f1.giveawayDetails.ContainsKey(dex))
                                {
                                    Program.f1.giveawayDetails.TryGetValue(dex, out details);

                                    if (details.Item1 == "")
                                    {
                                        string szNickname = Encoding.Unicode.GetString(block, 0x4, 24).Substring(2).Trim('\0'); //fix to prevent nickname clipping. Count should be 24, 2 bytes per letter, 2x12=24, not 20.

                                        string szFileToFind = details.Item2 + szNickname + ".pk7";
                                        if (!File.Exists(szFileToFind))
                                        {
                                            continue;
                                        }
                                    }
                                    Array.Copy(block, 0x3C, principal, 0, 4);
                                    byte check = Program.f1.calculateChecksum(principal);
                                    byte[] friendcode = new byte[8];
                                    Array.Copy(principal, 0, friendcode, 0, 4);
                                    friendcode[4] = check;
                                    long i_FC = BitConverter.ToInt64(friendcode, 0);
                                    szFC = i_FC.ToString().PadLeft(12, '0');

                                    int gender = block[0x2];
                                    int level = block[0x3];
                                    if ((gender == 0 || gender == details.Item3) && (level == 0 || level == details.Item4))
                                    {

                                        szTrainerName = Encoding.Unicode.GetString(block, 0x40, 24).Trim('\0');
                                        //Phrase = Encoding.Unicode.GetString(block, 0x5A, 30).Trim('\0');
                                        int countryIndex = BitConverter.ToInt16(block, 0x2F);
                                        string country = "-";
                                        Program.f1.countries.TryGetValue(countryIndex, out country);
                                        Program.f1.getSubRegions(countryIndex);
                                        int subRegionIndex = BitConverter.ToInt16(block, 0x31);
                                        string subregion = "-";
                                        Program.f1.regions.TryGetValue(subRegionIndex, out subregion);


                                        if (useLedySync && !Program.f1.banlist.Contains(szFC) && canThisTrade(principal, consoleName, szTrainerName, country, subregion, Program.PKTable.Species6[dex - 1], szFC, PageIndex + "", (i - 1) + ""))
                                        {
                                            PokemonFound = true;
                                            CurrentView = (uint)i;
                                            Program.f1.ChangeStatus("Found a pokemon to trade");
                                            botState = (int)gtsbotstates.trade;
                                            break;
                                        }
                                        else if (!useLedySync)
                                        {
                                            if ((!bReddit || Program.f1.commented.Contains(szFC)) && !details.Item6.Contains(BitConverter.ToInt32(principal, 0)) && !Program.f1.banlist.Contains(szFC))
                                            {
                                                PokemonFound = true;
                                                CurrentView = (uint)i;
                                                Program.f1.ChangeStatus("Found a pokemon to trade");
                                                botState = (int)gtsbotstates.trade;
                                                break;
                                            }
                                        }
                                    }
                                }


                                if (searchDirection == SEARCHDIRECTION_FROMBACK && startIndex > 100 && i * iDirection < iEndIndex)
                                {
                                    Program.f1.ChangeStatus("No pokemon to trade on this page, try previous page");
                                    Program.helper.quickbuton(Program.PKTable.DpadLEFT, commandtime);
                                    await Task.Delay(commandtime + delaytime + 1000);
                                    startIndex -= 200;
                                    await Program.helper.waitNTRwrite(GTSPageIndex, (uint)startIndex, iPID);
                                    await Task.Delay(commandtime + delaytime + 1000);
                                    Program.helper.quickbuton(Program.PKTable.DpadRIGHT, commandtime);
                                    await Task.Delay(commandtime + delaytime + 1000);
                                    await Program.helper.waitNTRread(GTSPageSize);
                                    i = (int)Program.helper.lastRead;

                                }
                                PokemonFound = false;
                            }


                            // No Pokemon found, return to Seek/Deposit Screen
                            if (!PokemonFound)
                            {
                                Program.f1.ChangeStatus("No Pokemon Found");
                                botState = (int)gtsbotstates.research;
                                break;
                            }
                            break;

                        case (int)gtsbotstates.trade:
                            Program.f1.ChangeStatus("Found a pokemon to trade");

                            waitTaskbool = Program.helper.waitNTRread(GTSPageIndex);
                            if (await waitTaskbool)
                            {

                                string szNickname = Encoding.Unicode.GetString(block, 0x4, 24).Substring(2).Trim('\0'); //fix to prevent nickname clipping. Count should be 24, 2 bytes per letter, 2x12=24, not 20.

                                string szPath = details.Item1;
                                string szFileToFind = details.Item2 + szNickname + ".pk7";
                                if (File.Exists(szFileToFind))
                                {
                                    szPath = szFileToFind;
                                }

                                string szTrainerName = Encoding.Unicode.GetString(block, 0x40, 24).Trim('\0');
                                //Phrase = Encoding.Unicode.GetString(block, 0x5A, 30).Trim('\0');
                                int countryIndex = BitConverter.ToInt16(block, 0x30);
                                string country = "-";
                                Program.f1.countries.TryGetValue(countryIndex, out country);
                                Program.f1.getSubRegions(countryIndex);
                                int subRegionIndex = BitConverter.ToInt16(block, 0x30);
                                string subregion = "-";
                                Program.f1.regions.TryGetValue(subRegionIndex, out subregion);

                                if (bBlacklist)
                                {
                                    details.Item6.Add(BitConverter.ToInt32(principal, 0));
                                }

                                //Inject Pokemon to Box1 Slot1
                                byte[] pkmEncrypted = System.IO.File.ReadAllBytes(szPath);
                                byte[] cloneshort = PKHeX.encryptArray(pkmEncrypted.Take(232).ToArray());
                                string ek7 = BitConverter.ToString(cloneshort).Replace("-", ", 0x");
                                Program.scriptHelper.write(BoxInject, cloneshort, iPID);

                                await Program.helper.waitNTRread(GTSPageIndex);
                                PageIndex = (Program.helper.lastRead + 1);


                                Program.f1.AppendListViewItem(szTrainerName, szNickname, country, subregion, Program.PKTable.Species6[dex - 1], szFC, (PageIndex / 100).ToString(), CurrentView.ToString());

                                // Open Settings, write CurrentView Index to wanted, return.
                                Program.helper.quickbuton(Program.PKTable.keyY, 200);
                                await Task.Delay(1200);
                                await Program.helper.waitNTRwrite(GTSCurrentView, (uint)CurrentView, iPID);
                                Program.helper.quickbuton(Program.PKTable.keyB, 200);
                                await Task.Delay(1500);
                                Program.helper.quickbuton(Program.PKTable.keyA, 200);
                                await Task.Delay(3000);
                                Program.helper.quickbuton(Program.PKTable.keyA, 200);
                                await Task.Delay(1500);
                                Program.helper.quickbuton(Program.PKTable.keyA, 200);
                                await Task.Delay(1500);
                                Program.helper.quickbuton(Program.PKTable.keyA, 200);
                                await Task.Delay(1500);

                                //Now we have the right Entry, enter current viewed Entry
                                Program.helper.quickbuton(Program.PKTable.keyA, 200);
                                await Task.Delay(500);
                                Program.helper.quickbuton(Program.PKTable.keyA, 200);
                                await Task.Delay(500);
                                Program.helper.quickbuton(Program.PKTable.keyA, 200);
                                await Task.Delay(500);
                                Program.helper.quickbuton(Program.PKTable.keyA, 200);
                                Program.f1.ChangeStatus("Trading pokemon on page " + (PageIndex / 100).ToString() + " index " + CurrentView.ToString() + "");
                                await Task.Delay(10000);

                                if (details.Item5 > 0)
                                {
                                    Program.f1.giveawayDetails[dex] = new Tuple<string, string, int, int, int, ArrayList>(details.Item1, details.Item2, details.Item3, details.Item4, details.Item5 - 1, details.Item6);
                                    foreach (System.Data.DataRow row in Program.gd.details.Rows)
                                    {
                                        if (row[0].ToString() == dex.ToString())
                                        {
                                            int count = int.Parse(row[5].ToString()) - 1;
                                            row[5] = count;
                                            break;
                                        }
                                    }
                                }

                                foreach (System.Data.DataRow row in Program.gd.details.Rows)
                                {
                                    if (row[0].ToString() == dex.ToString())
                                    {
                                        int amount = int.Parse(row[6].ToString()) + 1;
                                        row[6] = amount;
                                        break;
                                    }
                                }

                                //In Case the Pokemon is already traded, go back to Seek/Deposit Screen
                                Program.helper.quickbuton(Program.PKTable.keyB, 250);
                                await Task.Delay(1000);
                                Program.helper.quickbuton(Program.PKTable.keyB, 250);
                                await Task.Delay(1000);
                                // wait if trade is finished
                                await Task.Delay(35000);
                                bool cont = false;

                                foreach (KeyValuePair<int, Tuple<string, string, int, int, int, ArrayList>> pair in Program.f1.giveawayDetails)
                                {
                                    if (pair.Value.Item5 != 0)
                                    {
                                        cont = true;
                                        break;
                                    }
                                }
                                if (!cont)
                                {
                                    botresult = 1;
                                    botState = (int)gtsbotstates.botexit;
                                    break;
                                }

                                startIndex = 0;
                                CurrentView = 0;

                                foundLastPage = false;
                                PokemonFound = false;

                                if (bReddit)
                                {
                                    botState = (int)gtsbotstates.updatecomments;
                                }
                                else
                                {
                                    botState = (int)gtsbotstates.botstart;
                                    break;
                                }
                            }
                            break;

                        case (int)gtsbotstates.research:
                            Program.helper.quickbuton(Program.PKTable.keyB, 250);
                            await Task.Delay(3000);
                            Program.helper.quickbuton(Program.PKTable.keyB, 250);
                            await Task.Delay(3000);
                            botState = (int)gtsbotstates.pressSeek;
                            break;

                        case (int)gtsbotstates.botexit:
                            Program.f1.ChangeStatus("Stopped");
                            botstop = true;
                            break;
                        case (int)gtsbotstates.panic:
                            if (!Program.Connected)
                            {
                                Program.scriptHelper.connect(szIP, 8000);
                            }
                            Program.f1.ChangeStatus("Recovery Mode");

                            //In case of a Communication Error
                            Program.helper.quicktouch(50, 0, commandtime);
                            await Task.Delay(250);
                            Program.helper.quickbuton(Program.PKTable.keySELECT, commandtime);

                            //Check if Connected
                            waitTaskbool = Program.helper.waitNTRread(IsConnected);
                            if (await waitTaskbool)
                            {
                                if (Program.helper.lastRead == 0)
                                {
                                    Program.f1.ChangeStatus("Recovery Mode - lost connection, trying to reconnect...");
                                    Program.helper.quicktouch(235, 5, commandtime);
                                    await Task.Delay(2000);
                                    Program.helper.quicktouch(150, 140, commandtime);
                                    await Task.Delay(3000);
                                    Program.helper.quickbuton(Program.PKTable.keyA, commandtime);
                                    await Task.Delay(30000);
                                    //Disconnected
                                }
                            }


                            await Program.helper.waitNTRread(PSSMenuOFF);

                            //Re-Enter GTS
                            if (Program.helper.lastRead == PSSMenuIN)
                            {
                                Program.f1.ChangeStatus("Recovery Mode - PSS Menu detected, re-enter GTS...");
                                Program.helper.quicktouch(100, 50, commandtime);
                                await Task.Delay(3000);
                                Program.helper.quickbuton(Program.PKTable.keyA, commandtime);
                                Program.helper.quickbuton(Program.PKTable.keyA, commandtime);
                                await Task.Delay(15000);
                                botState = (int)gtsbotstates.botstart;
                                break;
                            }

                            await Program.helper.waitNTRread(currentScreen);

                            if ((int)Program.helper.lastRead == GTSScreen)
                            {
                                Program.f1.ChangeStatus("Recovery Mode - GTS Screen Detected!");
                                Program.helper.quickbuton(Program.PKTable.keyB, 250);
                                await Task.Delay(2000);
                                Program.helper.quickbuton(Program.PKTable.keyB, 250);
                                await Task.Delay(2000);
                                botState = (int)gtsbotstates.botstart;
                                break;
                            }

                            if ((int)Program.helper.lastRead == SearchScreen)
                            {
                                Program.f1.ChangeStatus("Recovery Mode - Search Screen Detected!");
                                Program.helper.quickbuton(Program.PKTable.keyB, 250);
                                await Task.Delay(2000);
                                botState = (int)gtsbotstates.botstart;
                                break;
                            }

                            if ((int)Program.helper.lastRead == SeekDepositScreen)
                            {
                                Program.f1.ChangeStatus("Recovery Mode - Seek/Deposit Screen Detected!");
                                botState = (int)gtsbotstates.botstart;
                                break;
                            }

                            if ((int)Program.helper.lastRead == iPokemonToFind)
                            {
                                Program.f1.ChangeStatus("Recovery Mode - Search Screen Detected!");
                                Program.helper.quicktouch(50, 0, commandtime);
                                await Task.Delay(2000);
                                Program.helper.quickbuton(Program.PKTable.keyB, 250);
                                await Task.Delay(2000);
                                botState = (int)gtsbotstates.botstart;
                                break;
                            }

                            // Spam B to get out of GTS
                            for (int i = 0; i < 20; i++)
                            {
                                Program.f1.ChangeStatus("Recovery Mode - trying return to PSS Menu...");
                                Program.helper.quickbuton(Program.PKTable.keyB, commandtime + 200);
                                await Task.Delay(500);
                            }

                            await Task.Delay(10000);
                            botState = (int)gtsbotstates.botstart;
                            break;

                        default:
                            botresult = -1;
                            botstop = true;
                            break;
                    }
                }
            }
            catch
            {
                botState = (int)gtsbotstates.panic;
            }
            if (this.serverEndPoint != null)
            {
                client.Close();
            }
            return botresult;
        }

        public void RequestStop()
        {
            botstop = true;
        }


    }
}
