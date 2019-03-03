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

        uint GTSPageSize;
        uint GTSPageIndex;
        uint GTSCurrentView;

        uint GTSListBlock;

        uint GTSBlockAll;
        uint GTSBlockStart;
        uint GTSBlockEnd;

        uint GTSBlockEntrySize;

        uint BoxInject;

        uint BoxScreen;

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
        private string szPath;
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

        private int listlength = 0;
        private int startIndex = 100;
        private byte[] blockbytes = new byte[256];
        private byte[] block = new byte[256];

        private int tradeIndex = -1;
        private uint addr_PageEntry = 0;
        private bool foundLastPage = false;

        private Tuple<string, string, int, int, int, ArrayList> details;
        private short dex;
        private string szNickname;
        private string country;
        private string subregion;
        private int iStartIndex;
        private int iEndIndex;
        private int iDirection;

        public string szTrainerName { get; private set; }
        public string Phrase { get; private set; }
        public string Message { get; private set; }

        private async Task<bool> isCorrectWindow(int expectedScreen)
        {
            await Task.Delay(o3dswaittime);
            await Program.helper.waitNTRread(0x00);
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

        public GTSBot6(int iP, int iPtF, int iPtFGender, int iPtFLevel, bool bBlacklist, bool bReddit, int iSearchDirection, string waittime, string consoleName, bool useLedySync, string ledySyncIp, string ledySyncPort, int game,string szIP)
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
                GTSPageSize = 0x08C69D698;
                GTSPageIndex = 0x08C6945C;
                GTSCurrentView = 0x08C6D6AC;

                GTSListBlock = 0x8C694F8;
                GTSBlockEntrySize = 0xA0;

                BoxInject = 0x8C9E134;

                BoxScreen = 0x1311B30;

                PokemonToFind = 0x08335290;
                PokemonToFindLevel = 0x08335298;
                PokemonToFindGender = 0x08335294;
            }
            if (game == 4) // X and Y
            {
                GTSPageSize = 0x08C66080;
                GTSPageIndex = 0x08C61E40;
                GTSCurrentView = 0x08C66090;

                GTSListBlock = 0x8C61EDC;
                GTSBlockEntrySize = 0xA0;
                //GTSBlockStart = 0x8C6656C;
                //GTSBlockEnd = 0x8C66568;
                //GTSBlockAll = 0x8C666A0;

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
                            await Program.helper.waitbutton(Program.PKTable.keyA);
                            await Task.Delay(1000);
                            botState = (int)gtsbotstates.startsearch;
                            break;

                        case (int)gtsbotstates.startsearch:
                            //Write wanted Pokemon, Level, Gender to Ram, won't Display it but works. ¯\_(ツ)_/¯
                            Program.f1.ChangeStatus("Setting Pokemon to find");
                            waitTaskbool = Program.helper.waitNTRwrite(PokemonToFind, pokemonIndex, iPID);
                            waitTaskbool = Program.helper.waitNTRwrite(PokemonToFindGender, pokemonGender, iPID);
                            waitTaskbool = Program.helper.waitNTRwrite(PokemonToFindLevel, pokemonLevel, iPID);
                            botState = (int)gtsbotstates.presssearch;
                            break;

                        case (int)gtsbotstates.presssearch:
                            Program.f1.ChangeStatus("Pressing seek button");
                            Program.helper.quicktouch(200, 180, commandtime);

                            if (searchDirection == SEARCHDIRECTION_FROMBACK)
                            {
                                //This is the only Solution i found so far to change the PageIndex, write Value while Loading the Frame.
                                await Task.Delay(1000);
                                await Program.helper.waitNTRwrite(GTSPageIndex, (uint)startIndex, iPID);
                            }

                            await Task.Delay(4000);
                            botState = (int)gtsbotstates.findPokemon;
                            break;

                        case (int)gtsbotstates.findPokemon:

                             await Program.helper.waitNTRread(BoxScreen);
                                if (Program.helper.lastRead.ToString() == "65794")
                                {
                                   botState = (int)gtsbotstates.panic;
                                    break;
                                }
                            


                            await Program.helper.waitNTRread(GTSPageSize);
                            uint Entries = (Program.helper.lastRead - 1);
                            CurrentView = Entries;

                            if (searchDirection == SEARCHDIRECTION_FROMBACK)
                            {
                                // Change current Page, everytime + 100
                                while (!foundLastPage)
                                {
                                    startIndex += 100;
                                    await Program.helper.waitNTRwrite(GTSPageIndex, (uint)startIndex, iPID);
                                    Program.f1.ChangeStatus("Moving to last page");
                                    Program.helper.quickbuton(Program.PKTable.DpadLEFT, commandtime);
                                    await Task.Delay(commandtime + delaytime + 1000);
                                    Program.helper.quickbuton(Program.PKTable.DpadRIGHT, commandtime);
                                    await Task.Delay(commandtime + delaytime + 1000);
                                    await Program.helper.waitNTRread(GTSPageSize);
                                    Entries = (Program.helper.lastRead - 1);

                                    if (Entries < 99 || startIndex >= 500) // Hardcoded to 5 Pages
                                    {
                                        foundLastPage = true;
                                        CurrentView = Entries;
                                    }
                                }
                            }

                            Program.f1.ChangeStatus("Looking for a Pokemon to Trade");

                            // Check the Trade Direction Back to Front or Front to Back
                            if (searchDirection == SEARCHDIRECTION_FROMBACK || searchDirection == SEARCHDIRECTION_FROMBACKFIRSTPAGEONLY)
                            {
                                CurrentView = Entries;
                                iStartIndex = (int)Entries;
                                iEndIndex = 0;
                                iDirection = -1;
                            }
                            else
                            {
                                CurrentView = 0;
                                iStartIndex = 1;
                                iEndIndex = (int)Entries + 1;
                                iDirection = 1;
                            }

                            //There's no Address for ALL Page Entries, so it takes alot longer to find a Pokemon. (2min from Entry 100-1)

                            for (int i = iStartIndex; i * iDirection < iEndIndex; i += iDirection)
                            {
                                //Get the Current Entry Data
                                waitTaskbool = Program.helper.waitNTRread(GTSListBlock + (CurrentView * GTSBlockEntrySize), (uint)(256 * 100));
                                if (await waitTaskbool)
                                {
                                    block = Program.helper.lastArray;
                                    //Collect Data
                                    int gender = block[0x2];
                                    int level = block[0x3];
                                    // int dlevel = block[0x7];
                                    dex = BitConverter.ToInt16(block, 0x0);
                                    // int Item = BitConverter.ToInt16(block, 0x22);
                                    szNickname = Encoding.Unicode.GetString(block, 0x08, 24).Trim('\0');
                                    szTrainerName = Encoding.Unicode.GetString(block, 0x40, 24).Trim('\0');
                                    // Phrase = Encoding.Unicode.GetString(block, 0x5A, 30).Trim('\0');
                                    //int countryIndex = BitConverter.ToInt16(block, 0x48);
                                    country = "-"; // No valid Country Array found :/
                                                   //Program.f1.countries.TryGetValue(countryIndex, out country);
                                                   //Program.f1.getSubRegions(countryIndex);
                                                   //int subRegionIndex = BitConverter.ToInt16(block, 0x236);
                                    subregion = "-"; // No valid Sub Region Array found :/
                                                     //Program.f1.regions.TryGetValue(subRegionIndex, out subregion);
                                    Array.Copy(block, 0x3C, principal, 0, 4);
                                    byte check = Program.f1.calculateChecksum(principal);
                                    byte[] friendcode = new byte[8];
                                    Array.Copy(principal, 0, friendcode, 0, 4);
                                    friendcode[4] = check;
                                    long i_FC = BitConverter.ToInt64(friendcode, 0);
                                    szFC = i_FC.ToString().PadLeft(12, '0');

                                    if (Program.f1.giveawayDetails.ContainsKey(dex))
                                    {
                                        Program.f1.giveawayDetails.TryGetValue(dex, out details);

                                        if ((gender == 0 || gender == details.Item3) && (level == 0 || level == details.Item4))
                                        {

                                            if (useLedySync && !Program.f1.banlist.Contains(szFC) && canThisTrade(principal, consoleName, szTrainerName, country, subregion, Program.PKTable.Species7[dex - 1], szFC, PageIndex + "", (i - 1) + ""))
                                            {
                                                //Check if Pokemon is from Gen7
                                                szPath = details.Item1;
                                                PKHeX dump = new PKHeX();
                                                dump.Data = PKHeX.decryptArray(System.IO.File.ReadAllBytes(szPath));
                                                if (dex < 721 || dump.Version < 29)
                                                {
                                                    PokemonFound = true;
                                                    Program.f1.ChangeStatus("Found a pokemon to trade");
                                                    botState = (int)gtsbotstates.trade;
                                                    break;
                                                }
                                                else
                                                {
                                                    Program.f1.ChangeStatus("Looking for a Pokemon to Trade, Current Entry: " + (CurrentView) + "/" + (Entries));
                                                    CurrentView--;
                                                }
                                            }
                                            else if (!useLedySync)
                                            {
                                                if ((!bReddit || Program.f1.commented.Contains(szFC)) && !details.Item6.Contains(BitConverter.ToInt32(principal, 0)) && !Program.f1.banlist.Contains(szFC))
                                                {
                                                    szPath = details.Item1;
                                                    //Check if Pokemon is from Gen7
                                                    szPath = details.Item1;
                                                    PKHeX dump = new PKHeX();
                                                    dump.Data = PKHeX.decryptArray(System.IO.File.ReadAllBytes(szPath));
                                                    if (dex < 721 || dump.Version < 29)
                                                    {
                                                        PokemonFound = true;
                                                        botState = (int)gtsbotstates.trade;
                                                        break;
                                                    }
                                                    else
                                                    {
                                                        Program.f1.ChangeStatus("Looking for a Pokemon to Trade, Current Entry: " + (CurrentView) + "/" + (Entries));
                                                        CurrentView--;
                                                    }
                                                }
                                            }
                                        }
                                        {
                                            Program.f1.ChangeStatus("Looking for a Pokemon to Trade, Current Entry: " + (CurrentView) + "/" + (Entries));
                                            CurrentView--;
                                        }
                                    }
                                    else
                                    {
                                        Program.f1.ChangeStatus("Looking for a Pokemon to Trade, Current Entry: " + (CurrentView) + "/" + (Entries));
                                        CurrentView--;
                                    }
                                }

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
                            // Trade Process
                            Program.f1.ChangeStatus("Found a pokemon to trade");
                            //Inject Pokemon to Box1 Slot1
                            byte[] pkmEncrypted = System.IO.File.ReadAllBytes(szPath);
                            byte[] cloneshort = PKHeX.encryptArray(pkmEncrypted.Take(232).ToArray());
                            string ek7 = BitConverter.ToString(cloneshort).Replace("-", ", 0x");
                            Program.scriptHelper.write(BoxInject, cloneshort, iPID);

                            await Program.helper.waitNTRread(GTSPageIndex);
                            PageIndex = (Program.helper.lastRead + 1);
                            Program.f1.AppendListViewItem(szTrainerName, szNickname, country, subregion, Program.PKTable.Species6[dex - 1], szFC, (PageIndex / 100).ToString(), CurrentView.ToString());

                            //Ghetto Solution \o/
                            //Enter current viewed Entry, write wanted current view to RAM, quit current viewed Entry
                            Program.helper.quickbuton(Program.PKTable.keyA, 200);
                            await Task.Delay(2200);
                            await Program.helper.waitNTRwrite(GTSCurrentView, (uint)CurrentView, iPID);
                            Program.helper.quickbuton(Program.PKTable.keyB, 200);
                            await Task.Delay(500);
                            Program.helper.quickbuton(Program.PKTable.keyB, 200);
                            await Task.Delay(2000);
                            Program.helper.quickbuton(Program.PKTable.keyA, 200);
                            await Task.Delay(3000);

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

                            //In Case the Pokemon is already traded, go back to Seek/Deposit Screen
                            Program.helper.quickbuton(Program.PKTable.keyB, 250);
                            await Task.Delay(1000);
                            Program.helper.quickbuton(Program.PKTable.keyB, 250);
                            await Task.Delay(1000);
                            // wait if trade is finished
                            await Task.Delay(35000);
                            PokemonFound = true;
                            startIndex = 100;
                            botState = (int)gtsbotstates.botstart;
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
                            Program.helper.quickbuton(Program.PKTable.keyA, 250);
                            await Task.Delay(5000);

                            // Spam B to get out of GTS
                            for (int i = 0; i < 15; i++)
                            {
                                Program.helper.quickbuton(Program.PKTable.keyB, commandtime + 200);
                                await Task.Delay(2000);
                            }
                           

                            Program.helper.quicktouch(170, 2, 200);
                            Program.helper.quicktouch(100, 50, 200);
                            Program.helper.quickbuton(Program.PKTable.keyA, 250);
                            Program.helper.quickbuton(Program.PKTable.keyA, 250);
                            Program.helper.quickbuton(Program.PKTable.keyA, 250);
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
