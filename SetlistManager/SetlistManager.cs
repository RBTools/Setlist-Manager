﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using SetlistManager.Properties;
using Application = System.Windows.Forms.Application;
using Button = System.Windows.Forms.Button;
using Path = System.IO.Path;
using Point = System.Drawing.Point;

namespace SetlistManager
{
    public partial class SetlistManager : Form
    {
        private readonly MyTools Tools;
        private readonly DTAParser Parser;
        private readonly string setlist_folder;
        public List<SongData> Songs;
        private readonly List<int> duplicates;
        private int ActiveSortColumn;
        public SortOrder ListSorting;
        private string ActiveConsole;
        private bool wait;
        private bool reset;
        private int mouseX;
        private int mouseY;
        private readonly string config;
        private string ActiveSetlistPath;
        private string ActiveSetlist;
        private bool isNewSong;
        private bool unSaved;
        private readonly List<Panel> FormPanels;
        private Color AlternateColor;
        private readonly string arguments;
        private bool findUnsupported;
        private bool findExactDup;
        private bool findPossDup;
        private bool findDoNotExport;
        private bool isLocked;
        private string LockPass;
        private static Color mMenuBackground;
        private readonly string binFolder;
        private const string AppName = "Setlist Manager";
        private Image RESOURCE_DIFF_NOPART;
        private Image RESOURCE_DIFF_0;
        private Image RESOURCE_DIFF_1;
        private Image RESOURCE_DIFF_2;
        private Image RESOURCE_DIFF_3;
        private Image RESOURCE_DIFF_4;
        private Image RESOURCE_DIFF_5;
        private Image RESOURCE_DIFF_6;
        private readonly List<PictureBox> diffBoxes;
        private bool showMessage;
        private bool isSearchingForDups;
        private readonly List<ColumnInfo> Columns;
        private readonly List<CheckBox> ColumnBoxes;

        public SetlistManager(string args = "")
        {
            InitializeComponent();
            mMenuBackground = menuStrip1.BackColor;
            menuStrip1.Renderer = new DarkRenderer();
            contextMenuStrip1.Renderer = new DarkRenderer();
            Tools = new MyTools();
            Parser  = new DTAParser();
            Songs = new List<SongData>();
            duplicates = new List<int>();
            binFolder = Application.StartupPath + "\\bin\\";
            if (!Directory.Exists(binFolder))
            {
                Directory.CreateDirectory(binFolder);
            }
            FormPanels = new List<Panel>
                {
                    PanelSource,
                    PanelDecades,
                    PanelCount,
                    PanelMaster,
                    PanelRating,
                    PanelGenre,
                    PanelGender,
                    PanelDuration,
                    PanelSearch,
                    PanelInstruments,
                    PanelInfo
                };

            setlist_folder = Application.StartupPath + "\\setlist\\";
            if (!Directory.Exists(setlist_folder))
            {
                Directory.CreateDirectory(setlist_folder);
            }

            Columns = new List<ColumnInfo>();
            for (var i = 0; i < lstSongs.Columns.Count; i++)
            {
                Columns.Add(new ColumnInfo { Visible = true, origWidth = lstSongs.Columns[i].Width});
            }
            ColumnBoxes = new List<CheckBox>
            {
                chkColSong, chkColArtist, chkColMaster, chkColAlbum, chkColTrack,
                chkColVocalParts, chkColGenre, chkColSinger, chkColRating, chkColYear,
                chkColDuration, chkColSource, chkColGuitar, chkColBass, chkColKeys,
                chkColDrums, chkColVocals, chkColBand, chkColLink, chkColProGuitar,
                chkColProBass, chkColProKeys,
            };
            AlternateColor = Color.AliceBlue;
            ActiveConsole = "Xbox 360";
            config = binFolder + "setlist.config";
            ListSorting = SortOrder.Ascending;
            ActiveSortColumn = 0; //artist

            wait = true;
            cboGenre.SelectedIndex = 0;
            cboTime.SelectedIndex = 0;
            wait = false;

            picWorking.Parent = lstSongs;
            picWorking.BringToFront();
            arguments = args;
            
            DoubleBuffered(lstSongs, true);
            diffBoxes = new List<PictureBox> {diffGuitar, diffProGuitar, diffBass, diffProBass, diffDrums, diffKeys, diffProKeys, diffVocals, diffBand};
            loadImages();
        }

        private void loadImages()
        {
            try
            {
                RESOURCE_DIFF_0 = Tools.LoadImage(Application.StartupPath + "\\res\\diff0.png");
                RESOURCE_DIFF_1 = Tools.LoadImage(Application.StartupPath + "\\res\\diff1.png");
                RESOURCE_DIFF_2 = Tools.LoadImage(Application.StartupPath + "\\res\\diff2.png");
                RESOURCE_DIFF_3 = Tools.LoadImage(Application.StartupPath + "\\res\\diff3.png");
                RESOURCE_DIFF_4 = Tools.LoadImage(Application.StartupPath + "\\res\\diff4.png");
                RESOURCE_DIFF_5 = Tools.LoadImage(Application.StartupPath + "\\res\\diff5.png");
                RESOURCE_DIFF_6 = Tools.LoadImage(Application.StartupPath + "\\res\\diff6.png");
                RESOURCE_DIFF_NOPART = Tools.LoadImage(Application.StartupPath + "\\res\\nopart.png");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading one or more of the resource images:\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void ChangeDifficulty_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (isLocked)
            {
                DoUnlockWarning();
                return;
            }
            var instrument = (PictureBox)sender;
            if (instrument.Cursor != Cursors.Hand) return;
            MoveMarker(sender);
            var currDiff = Tools.GetDiffTag(instrument);
            var popup = new DifficultySelector(Cursor.Position, currDiff);
            popup.ShowDialog();
            var newDiff = popup.Difficulty;
            popup.Dispose();
            if (currDiff == newDiff) return;
            SetDifficulty(instrument, newDiff);
            InfoChanged(null, null);
        }

        private void ClearDiffBoxes(bool isNoPart)
        {
            foreach (var box in diffBoxes)
            {
                box.Image = isNoPart ? RESOURCE_DIFF_NOPART : null;
            }
        }

        public static void DoubleBuffered(Control control, bool enable)
        {
            var doubleBufferPropertyInfo = control.GetType().GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            doubleBufferPropertyInfo.SetValue(control, enable, null);
        }

        private void HandleDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.All;
        }

        private void HandleDragDrop(object sender, DragEventArgs e)
        {
            if (isLocked) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            ValidateFile(files, true);
        }

        private void ValidateFile(IList<string> files, bool AskUser = false)
        {
            switch (Path.GetExtension(files[0]))
            {
                case ".txt":
                    try
                    {
                        var sr = new StreamReader(files[0]);
                        var line = sr.ReadLine();
                        sr.Dispose();
                        if (line.Contains("xml")) goto case ".xml";
                    }
                    catch (Exception)
                    {}
                    MessageBox.Show("That's not a valid file to use here", AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                case ".xml":
                    ImportXML(files[0]);
                    break;
                case ".setlist":
                    if (string.IsNullOrEmpty(ActiveSetlist))
                    {
                        goto LoadSetlist;
                    }
                    if (AskUser)
                    {
                        var dialog = new SMDialog();
                        dialog.ShowDialog();
                        var action = dialog.UserAction;
                        dialog.Dispose();
                        switch (action)
                        {
                            case 0:
                                return;
                            case 1:
                                goto LoadSetlist;
                            case 2:
                                var songs = GrabSongsFromSetlist(files[0], false);
                                FinalizeImport(songs);
                                break;
                        }
                    }
                    else
                    {
                        goto LoadSetlist;
                    }
                    break;
                case ".config":
                    LoadOptions(files[0]);
                    break;
                case ".dta":
                    if (btnNew.Enabled)
                    {
                        if (Parser.ReadDTA(File.ReadAllBytes(files[0])))
                        {
                            PrepForNewSong();
                            FinalizeImport(Parser.Songs);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Create a Setlist first", AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                    break;
                default:
                    MessageBox.Show("That's not a valid file to use here", AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
            }
            return;
            LoadSetlist:
            if (!LoadSetlist(files[0])) return;
            if (LoadSongs())
            {
                SortSongs();
            }
        }

        private void SaveSetlist(string file)
        {
            SaveSetlist(file, Songs);
        }

        private void SaveSetlist(string file, ICollection<SongData> SongsToSave)
        {
            if (string.IsNullOrWhiteSpace(file)) return;
            dONOTPRINT.Enabled = false;

            try
            {
                var sw = new StreamWriter(file, false, Encoding.UTF8);
                sw.WriteLine("SetlistName=" + ActiveSetlist);
                sw.WriteLine("Console=" + ActiveConsole);
                sw.WriteLine("SongCount=" + SongsToSave.Count);
                sw.WriteLine("PackageCount=0");

                //only change if additions are made to the setlist in the future
                sw.WriteLine("CacheFormat=RB_5");

                foreach (var song in SongsToSave)
                {
                    //entries for CacheFormat 1
                    sw.WriteLine("Artist=" + song.Artist);
                    sw.WriteLine("Name=" + song.Name);
                    sw.WriteLine("Album=" + song.Album);
                    sw.WriteLine("TrackNumber=" + song.TrackNumber);
                    sw.WriteLine("Master=" + song.Master);
                    sw.WriteLine("YearRecorded=" + song.YearRecorded);
                    sw.WriteLine("YearReleased=" + song.YearReleased);
                    sw.WriteLine("Genre=" + song.Genre);
                    sw.WriteLine("Rating=" + song.Rating);
                    sw.WriteLine("VocalGender=" + song.GetGender());
                    sw.WriteLine("VocalParts=" + song.VocalParts);
                    sw.WriteLine("DrumsDiff=" + song.DrumsDiff);
                    sw.WriteLine("BassDiff=" + song.BassDiff);
                    sw.WriteLine("ProBassDiff=" + song.ProBassDiff);
                    sw.WriteLine("GuitarDiff=" + song.GuitarDiff);
                    sw.WriteLine("ProGuitarDiff=" + song.ProGuitarDiff);
                    sw.WriteLine("KeysDiff=" + song.KeysDiff);
                    sw.WriteLine("ProKeysDiff=" + song.ProKeysDiff);
                    sw.WriteLine("VocalsDiff=" + song.VocalsDiff);
                    sw.WriteLine("BandDiff=" + song.BandDiff);
                    sw.WriteLine("SongLength=" + song.Length);
                    sw.WriteLine("ShortName=" + song.ShortName);
                    sw.WriteLine("SongID=" + song.SongId);
                    sw.WriteLine("Source=" + song.Source);

                    //entries for CacheFormat 2
                    sw.WriteLine("SongPath=" + song.FilePath);
                    sw.WriteLine("PreviewStart=" + song.PreviewStart);
                    sw.WriteLine("PreviewEnd=" + song.PreviewEnd);
                    sw.WriteLine("GameVersion=" + song.GameVersion);
                    sw.WriteLine("ScrollSpeed=" + song.ScrollSpeed);
                    sw.WriteLine("TonicNote=" + song.TonicNote);
                    sw.WriteLine("Tonality=" + song.Tonality);
                    sw.WriteLine("PercussionBank=" + song.PercussionBank);
                    sw.WriteLine("DrumBank=" + song.DrumBank);

                    //entry for CacheFormat 3
                    sw.WriteLine("DoNotPrint=" + song.DoNotExport);

                    //entries for CacheFormat 4
                    sw.WriteLine("ProBassTuning=" + song.ProBassTuning);
                    sw.WriteLine("ProGuitarTuning=" + song.ProGuitarTuning);

                    //entry for CacheFormat 5
                    sw.WriteLine("SongLink=" + song.SongLink);

                    //if adding anything else, change CacheFormat for anything below this line
                }
                sw.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving Setlist '" + Path.GetFileName(file) + "':\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            dONOTPRINT.Enabled = true;
        }
        
        private void SetlistManager_Shown(object sender, EventArgs e)
        {
            Application.DoEvents();
            LoadOptions();
            doUpdateGameMode();
            downloader.RunWorkerAsync();
            updater.RunWorkerAsync();
        }
        
        private void SetlistManager_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (isLocked)
            {
                MessageBox.Show("You must unlock the program first", "Locked", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                lockToolStrip.BackColor = Color.Yellow;
                tmrHighlight.Enabled = true;
                e.Cancel = true;
                return;
            }
            if (unSaved)
            {
                if (MessageBox.Show("You have unsaved changes to your Setlist\nAre you sure you want to exit and lose those changes?\n\nClick NO to cancel and return to the program",
                        AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    e.Cancel = true;
                }
            }
            if (e.Cancel) return;
            SaveOptions();
        }

        private void SaveOptions()
        {
            var sw = new StreamWriter(config, false);
            sw.WriteLine("ActiveSetlist=" + ActiveSetlistPath);
            //menu options
            sw.WriteLine("NormalizeFeaturedArtists=" + normalizeFeaturedArtists.Checked);
            sw.WriteLine("MoveFeaturedArtists=" + moveFeaturedArtistsToSongName.Checked);
            sw.WriteLine("RemoveFeaturedArtists=" + removeFeaturedArtists.Checked);
            sw.WriteLine("RemoveLive=" + removeLiveFromSongNames.Checked);
            sw.WriteLine("RemoveRB3Version=" + removeRB3VersionFromNames.Checked);
            sw.WriteLine("Remove2XBass=" + remove2xBassPedalFromNames.Checked);
            sw.WriteLine("ShowInstrumentTiers=" + showInstrumentTiers.Checked);
            sw.WriteLine("AlternateArtists=" + alternateColorsForArtists.Checked);
            sw.WriteLine("AlternateAlbums=" + alternateColorsForAlbums.Checked);
            sw.WriteLine("AutoLoad=" + autoloadLastSetlistAtRunTime.Checked);
            //panels
            foreach (var panel in FormPanels)
            {
                sw.WriteLine(panel.Name + "Visible=" + panel.Visible);
                sw.WriteLine(panel.Name + "Left=" + panel.Left);
                sw.WriteLine(panel.Name + "Top=" + panel.Top);
                sw.WriteLine(panel.Name + "TextColor=#" + panel.ForeColor.A.ToString("X2") + panel.ForeColor.R.ToString("X2") + panel.ForeColor.G.ToString("X2") + panel.ForeColor.B.ToString("X2"));
                sw.WriteLine(panel.Name + "BackColor=#" + panel.BackColor.A.ToString("X2") + panel.BackColor.R.ToString("X2") + panel.BackColor.G.ToString("X2") + panel.BackColor.B.ToString("X2"));
            }
            sw.WriteLine("ListLeft=" + lstSongs.Left);
            sw.WriteLine("ListTop=" + lstSongs.Top);
            sw.WriteLine("ListHeight=" + lstSongs.Height);
            sw.WriteLine("ListTextColor=#" + lstSongs.ForeColor.A.ToString("X2") + lstSongs.ForeColor.R.ToString("X2") + lstSongs.ForeColor.G.ToString("X2") + lstSongs.ForeColor.B.ToString("X2"));
            sw.WriteLine("ListBackColor=#" + lstSongs.BackColor.A.ToString("X2") + lstSongs.BackColor.R.ToString("X2") + lstSongs.BackColor.G.ToString("X2") + lstSongs.BackColor.B.ToString("X2"));
            sw.WriteLine("ListAltColor=#" + AlternateColor.A.ToString("X2") + AlternateColor.R.ToString("X2") + AlternateColor.G.ToString("X2") + AlternateColor.B.ToString("X2"));
            sw.WriteLine("ConfirmBeforeDelete=" + confirmBeforeDeleting.Checked);
            sw.WriteLine("GameMode=" + (modeRB3.Checked ? 0 : (modeRB4.Checked ? 1 : 2)));
            sw.WriteLine("ListWidth=" + lstSongs.Width);
            sw.WriteLine("FormHeight=" + Height);
            sw.WriteLine("FormWidth=" + Width);
            for (var i = 0; i < Columns.Count; i++)
            {
                sw.WriteLine("Column" + i + "Index=" + lstSongs.Columns[i].DisplayIndex);
                sw.WriteLine("Column" + i + "Width=" + (Columns[i].currWidth > 0 ? Columns[i].currWidth : lstSongs.Columns[i].Width));
                sw.WriteLine("Column" + i + "Visible=" + Columns[i].Visible);
            }
            sw.WriteLine("UseTierNames=" + useTierNames.Checked);
            sw.WriteLine("UseTierNumbers=" + useTierNumbers.Checked);
            sw.WriteLine("UseTierDots=" + useTierDots.Checked);
            sw.Dispose();
        }

        private void LoadOptions(string path = "")
        {
            if (!File.Exists(config) && (string.IsNullOrWhiteSpace(path) || !File.Exists(path))) return;

            var sr = new StreamReader(string.IsNullOrWhiteSpace(path) ? config : path);

            try
            {
                ActiveSetlistPath = Tools.GetConfigString(sr.ReadLine());
                normalizeFeaturedArtists.Checked = sr.ReadLine().Contains("True");
                moveFeaturedArtistsToSongName.Checked = sr.ReadLine().Contains("True");
                removeFeaturedArtists.Checked = sr.ReadLine().Contains("True");
                removeLiveFromSongNames.Checked = sr.ReadLine().Contains("True");
                removeRB3VersionFromNames.Checked = sr.ReadLine().Contains("True");
                remove2xBassPedalFromNames.Checked = sr.ReadLine().Contains("True");
                if (sr.ReadLine().Contains("False")) showInstrumentTiers.PerformClick();
                alternateColorsForArtists.Checked = sr.ReadLine().Contains("True");
                alternateColorsForAlbums.Checked = sr.ReadLine().Contains("True");
                autoloadLastSetlistAtRunTime.Checked = sr.ReadLine().Contains("True");

                int x;
                int y;
                foreach (var panel in FormPanels)
                {
                    var line = sr.ReadLine();
                    if (line.Contains("PanelSong")) 
                    {
                        //no longer a valid panel, let's skip so users don't end up with wrong settings
                        sr.ReadLine();
                        sr.ReadLine();
                        sr.ReadLine();
                        sr.ReadLine();
                        line = sr.ReadLine();
                    }
                    panel.Visible = line.Contains("True");
                    x = Convert.ToInt16(Tools.GetConfigString(sr.ReadLine()));
                    y = Convert.ToInt16(Tools.GetConfigString(sr.ReadLine()));
                    panel.Location = new Point(x, y);
                    panel.ForeColor = ColorTranslator.FromHtml(Tools.GetConfigString(sr.ReadLine()));
                    panel.BackColor = ColorTranslator.FromHtml(Tools.GetConfigString(sr.ReadLine()));
                    ColorPanelText(panel,panel.ForeColor);
                }

                x = Convert.ToInt16(Tools.GetConfigString(sr.ReadLine()));
                y = Convert.ToInt16(Tools.GetConfigString(sr.ReadLine()));
                lstSongs.Location = new Point(x, y);
                lstSongs.Height = Convert.ToInt16(Tools.GetConfigString(sr.ReadLine()));
                lstSongs.ForeColor = ColorTranslator.FromHtml(Tools.GetConfigString(sr.ReadLine()));
                lstSongs.BackColor = ColorTranslator.FromHtml(Tools.GetConfigString(sr.ReadLine()));
                AlternateColor = ColorTranslator.FromHtml(Tools.GetConfigString(sr.ReadLine()));
                picWorking.Top = (lstSongs.Height - picWorking.Height)/2;

                confirmBeforeDeleting.Checked = sr.ReadLine().Contains("True");
                var mode = Convert.ToInt16(Tools.GetConfigString(sr.ReadLine()));
                switch (mode)
                {
                    case 0:
                        UpdateGameMode(modeRB3, null);
                        break;
                    case 1:
                        UpdateGameMode(modeRB4, null);
                        break;
                    default:
                        return;
                }
                lstSongs.Width = Convert.ToInt16(Tools.GetConfigString(sr.ReadLine()));
                Height = Convert.ToInt16(Tools.GetConfigString(sr.ReadLine()));
                Width = Convert.ToInt16(Tools.GetConfigString(sr.ReadLine()));
                for (var i = 0; i < Columns.Count; i++)
                {
                    lstSongs.Columns[i].DisplayIndex = Convert.ToInt16(Tools.GetConfigString(sr.ReadLine()));
                    Columns[i].currWidth = Convert.ToInt16(Tools.GetConfigString(sr.ReadLine()));
                    Columns[i].Visible = sr.ReadLine().Contains("True");
                    if (Columns[i].Visible && Columns[i].currWidth > 0)
                    {
                        lstSongs.Columns[i].Width = Columns[i].currWidth;
                    }
                    else if (!Columns[i].Visible)
                    {
                        lstSongs.Columns[i].Width = 0;
                    }
                }
                useTierNames.Checked = sr.ReadLine().Contains("True");
                useTierNumbers.Checked = sr.ReadLine().Contains("True");
                useTierDots.Checked = sr.ReadLine().Contains("True");
            }
            catch (Exception)
            {}
            sr.Dispose();
            Application.DoEvents();

            if (arguments != "")
            {
                if (arguments.ToLowerInvariant().EndsWith(".setlist", StringComparison.Ordinal))
                {
                    ActiveSetlistPath = arguments;
                }
                else
                {
                    return;
                }
            }
            else if (!autoloadLastSetlistAtRunTime.Checked)
            {
                ActiveSetlistPath = "";
                return;
            }
            if (!File.Exists(ActiveSetlistPath))
            {
                ActiveSetlistPath = "";
                return;
            }
            if (!LoadSetlist(ActiveSetlistPath)) return;
            LoadSongs();
        }
        
        private bool PassDecadeFilter(int year)
        {
            var passes = true;
            
            if (year > 2009 && !chkYear10.Checked)
            {
                passes = false;
            }
            else if (year > 1999 && year < 2010 && !chkYear00.Checked)
            {
                passes = false;
            }
            else if (year > 1989 && year < 2000 && !chkYear90.Checked)
            {
                passes = false;
            }
            else if (year > 1979 && year < 1990 && !chkYear80.Checked)
            {
                passes = false;
            }
            else if (year > 1969 && year < 1980 && !chkYear70.Checked)
            {
                passes = false;
            }
            else if (year > 1959 && year < 1970 && !chkYear60.Checked)
            {
                passes = false;
            }
            else if (year > 1949 && year < 1960 && !chkYear50.Checked)
            {
                passes = false;
            }
            else if (year < 1950 && !chkYearEarlier.Checked)
            {
                passes = false;
            }
            
            return passes;
        }

        private bool PassRatingFilter(string rating)
        {
            var ratings = (chkRatingFF.Checked ? "FF" : "") + (chkRatingSR.Checked ? "SR" : "") + (chkRatingM.Checked ? "M" : "") + (chkRatingNR.Checked ? "NR" : "");

            return ratings.Contains(rating);
        }

        private bool PassSourceFilter(string source)
        {
            var sources = (chkRB1.Checked ? "RB1" : "") + (chkRB2.Checked ? "RB2" : "") + (chkRB3.Checked ? "RB3" : "") +
                         (chkDLC.Checked ? "DLC" : "") + (chkRBN1.Checked ? "RBN1" : "") + (chkRBN2.Checked ? "RBN2" : "") +
                         (chkLego.Checked ? "Lego" : "") + (chkGDRB.Checked ? "GD:RB" : "") +
                         (chkACDC.Checked ? "AC/DC" : "") + (chkRB4.Checked ? "RB4" : "") + (chkBlitz.Checked ? "Blitz" : "");
            if (source.Contains("DLC"))
            {
                source = source.Replace("RB1", "").Replace("RB2", "").Replace("RB3", "").Replace("RB4", "").Trim();
            }
            return sources.Contains(source);
        }

        private bool PassDurationFilter(Int32 time)
        {
            var passes = true;
            var goal = numTime.Value*60000; //convert time selection to milliseconds
            switch (cboTime.SelectedIndex)
            {
                case 1: //shorter than
                    passes = time <= goal;
                    break;
                case 2: //longer than
                    passes = time >= goal;
                    break;
                case 3: //exactly
                    passes = time >= goal && (time - goal < 60000); //return when it's within the same minute
                    break;
                case 4: //around
                    passes = time >= goal - 90000 && time <= goal + 90000; //+- 1.5 minutes
                    break;
            }
            return passes;
        }

        private bool PassesFilters(SongData song)
        {
            var passes = true;
            var artist = CleanName(song.Artist).ToLowerInvariant();
            var name = CleanName(song.Name).ToLowerInvariant();
            if (moveFeaturedArtistsToSongName.Checked)
            {
                var featured = MoveFeatArtist(artist, name);
                if (featured.Count > 0)
                {
                    artist = featured[0];
                    name = featured[1];
                }
            }
            if (!chkMasterNo.Checked && !song.Master)
            {
                passes = false;
            }
            else if (!chkMasterYes.Checked && song.Master)
            {
                passes = false;
            }
            else if (!chkMale.Checked && song.GetGender() == "Male")
            {
                passes = false;
            }
            else if (!chkFemale.Checked && song.GetGender() == "Female")
            {
                passes = false;
            }
            else if (cboGenre.SelectedIndex > 0 && song.Genre != cboGenre.SelectedItem.ToString())
            {
                passes = false;
            }
            else if (!PassDecadeFilter(song.YearReleased))
            {
                passes = false;
            }
            else if (!PassRatingFilter(song.GetRating()))
            {
                passes = false;
            }
            else if (!PassSourceFilter(song.GetSource()))
            {
                passes = false;
            }
            else if (cboTime.SelectedIndex > 0 && !PassDurationFilter(song.Length))
            {
                passes = false;
            }
            else if (!PassInstrumentFilter(song))
            {
                passes = false;
            }
            else if (txtSearch.Text.Trim() != "")
            {
                var search = txtSearch.Text.ToLowerInvariant();
                var album = string.IsNullOrWhiteSpace(song.Album) ? "" : CleanName(song.Album.ToLowerInvariant());
                var genre = string.IsNullOrWhiteSpace(song.Genre) ? "" : CleanName(song.Genre.ToLowerInvariant());
                var subgenre = string.IsNullOrWhiteSpace(song.SubGenre) ? "" : CleanName(song.SubGenre.ToLowerInvariant());
                var year1 = song.YearReleased.ToString(CultureInfo.InvariantCulture);
                var year2 = song.YearRecorded.ToString(CultureInfo.InvariantCulture);
                
                if (radioArtist.Checked && !artist.Contains(search))
                {
                    passes = false;
                }
                else if (radioSong.Checked && !name.Contains(search))
                {
                    passes = false;
                }
                else if (radioAlbum.Checked && !album.Contains(search))
                {
                    passes = false;
                }
                else if (radioAny.Checked && !artist.Contains(search) && !name.Contains(search) && !album.Contains(search) &&
                    !genre.Contains(search) && !subgenre.Contains(search) && !year1.Contains(search) && !year2.Contains(search))
                {
                    passes = false;
                }
            }
            return passes;
        }

        private bool PassInstrumentFilter(SongData song)
        {
            var passes = true;
            if (chkDrums.Checked && song.DrumsDiff == 0)
            {
                passes = false;
            }
            else if (chk2X.Checked && !(song.Name.ToLowerInvariant().Contains("2x") && (song.Name.ToLowerInvariant().Contains("bass") || song.Name.ToLowerInvariant().Contains("pedal"))))
            {
                passes = false;
            }
            else if (chkBass.Checked && song.BassDiff == 0)
            {
                passes = false;
            }
            else if (chkProBass.Checked && song.ProBassDiff == 0)
            {
                passes = false;
            }
            else if (chkGuitar.Checked && song.GuitarDiff == 0)
            {
                passes = false;
            }
            else if (chkProGuitar.Checked && song.ProGuitarDiff == 0)
            {
                passes = false;
            }
            else if (chkKeys.Checked && song.KeysDiff == 0)
            {
                passes = false;
            }
            else if (chkKeys.Checked && song.KeysDiff > 0 && song.ProKeysDiff == 0 && (song.Name.ToLowerInvariant().Contains("rhythm") &&
                      (song.Name.ToLowerInvariant().Contains("guitar") || song.Name.ToLowerInvariant().Contains("version"))))
            {
                //don't return songs with "keys" that are really rhythm charts
                passes = false;
            }
            else if (chkProKeys.Checked && song.ProKeysDiff == 0)
            {
                passes = false;
            }
            else if ((chkVocals.Checked || chkHarm2.Checked || chkHarm3.Checked) && song.VocalsDiff == 0)
            {
                passes = false;
            }
            else if (chkVocals.Checked && song.VocalParts > 1)
            {
                if ((song.VocalParts == 2 && !chkHarm2.Checked) || (song.VocalParts == 3 && !chkHarm3.Checked))
                {
                    passes = false;
                }
            }
            else if (chkHarm2.Checked && song.VocalParts != 2)
            {
                if ((song.VocalParts == 1 && !chkVocals.Checked) || (song.VocalParts == 3 && !chkHarm3.Checked))
                {
                    passes = false;
                }
            }
            else if (chkHarm3.Checked && song.VocalParts != 3)
            {
                if ((song.VocalParts == 1 && !chkVocals.Checked) || (song.VocalParts == 2 && !chkHarm2.Checked))
                {
                    passes = false;
                }
            }
            return passes;
        }

        private string CleanName(string name, bool force = false)
        {
            var clean = name;
            if (normalizeFeaturedArtists.Checked || force || removeFeaturedArtists.Checked)
            {
                clean = Tools.FixFeaturedArtist(clean);
                if (removeFeaturedArtists.Checked || force)
                {
                    var index = -1;
                    if (clean.Contains("(ft."))
                    {
                        index = clean.IndexOf("(ft.", StringComparison.Ordinal);
                    }
                    if (clean.Contains("[ft."))
                    {
                        index = clean.IndexOf("[ft.", StringComparison.Ordinal);
                    }
                    else if (clean.Contains(" ft."))
                    {
                        index = clean.IndexOf(" ft.", StringComparison.Ordinal);
                    }
                    if (index > -1)
                    {
                        clean = clean.Substring(0, index).Trim();
                    }
                }
            }
            if (removeLiveFromSongNames.Checked || force)
            {
                clean = clean.Replace("(Live)", "").Trim();
                clean = clean.Replace("(live)", "").Trim();
            }
            if (remove2xBassPedalFromNames.Checked || force)
            {
                clean = clean.Replace("(2X Bass Pedal)", "").Trim();
                clean = clean.Replace("(2x Bass Pedal)", "").Trim();
                clean = clean.Replace("(2X Bass)", "").Trim();
                clean = clean.Replace("(2x Bass)", "").Trim();
                clean = clean.Replace("(2X Pedal)", "").Trim();
                clean = clean.Replace("(2x Pedal)", "").Trim();
            }
            if (removeRB3VersionFromNames.Checked || force)
            {
                clean = clean.Replace("(RB3 Version)", "").Trim();
                clean = clean.Replace("(RB3 version)", "").Trim();
            }
            return clean;
        }

        private List<string> MoveFeatArtist(string Artist, string Song)
        {
            var featured = new List<string>();
            try
            {
                var feat = Tools.FixFeaturedArtist(Artist);
                int index;
                if (feat.Contains(" ft."))
                {
                    index = feat.IndexOf(" ft.", StringComparison.Ordinal);
                }
                else if (feat.Contains(" (ft."))
                {
                    index = feat.IndexOf(" (ft.", StringComparison.Ordinal);
                }
                else
                {
                    index = -1;
                }
                if (index > -1)
                {
                    var artist2 = Artist.Substring(index, Artist.Length - index).Trim();
                    if (!artist2.StartsWith("(", StringComparison.Ordinal))
                    {
                        artist2 = "(" + artist2;
                    }
                    if (!artist2.EndsWith(")", StringComparison.Ordinal))
                    {
                        artist2 = artist2 + ")";
                    }
                    var name = Song + " " + artist2;
                    var artist = Artist.Substring(0, index);
                    featured.Add(artist);
                    featured.Add(name);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("There was an error:\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return featured;
        }
        
        private bool LoadSongs()
        {
            picWorking.Visible = true;
            picWorking.Refresh();
            lstSongs.ListViewItemSorter = null; //necessary to prevent auto-sorting as items are added
            lstSongs.Sorting = SortOrder.None;
            lstSongs.Items.Clear();
            ClearDetailedInfo();
            EnableDisableInfo(false);
            btnSave.Visible = false;
            btnDeleteSong.Enabled = false;
            var counter = -1;
            if (findExactDup || findPossDup)
            {
                FindDuplicates();
                duplicates.Clear();
                picWorking.Visible = false;
                return true;
            }
            for (var i = 0; i < Songs.Count; i++)
            {
                var song = Songs[i];
                counter++;
                if (isSearchingForDups)
                {
                    if (!duplicates.Contains(i)) continue;
                }
                else if (findUnsupported)
                {
                    var specials = new List<string> { "�", "ï¿½", "*", "Ã", "E½" }; //add any others here
                    var isSpecial = false;
                    foreach (var weirdo in specials.Where(weirdo => song.Artist.Contains(weirdo) || song.Name.Contains(weirdo) || song.Album.Contains(weirdo)))
                    {
                        isSpecial = true;
                    }
                    if (!isSpecial) continue;
                }
                else if (findDoNotExport)
                {
                    if (!song.DoNotExport) continue;
                }
                else
                {
                    if (!PassesFilters(song)) continue;
                }
                var artist = CleanName(song.Artist);
                var name = CleanName(song.Name);
                if (moveFeaturedArtistsToSongName.Checked)
                {
                    var featured = MoveFeatArtist(artist, name);
                    if (featured.Count > 0)
                    {
                        artist = featured[0];
                        name = featured[1];
                    }
                }
                var entry = new ListViewItem(artist);
                entry.SubItems.Add(name);
                entry.SubItems.Add(song.Master ? "✔" : "X");
                entry.SubItems.Add(song.Album);
                entry.SubItems.Add(song.YearReleased > 0 ? song.YearReleased.ToString(CultureInfo.InvariantCulture) : "");
                entry.SubItems.Add(song.TrackNumber > 0 ? song.TrackNumber.ToString(CultureInfo.InvariantCulture) : "");
                entry.SubItems.Add(song.Genre);
                entry.SubItems.Add(song.VocalsDiff == 0 ? "0" : song.VocalParts.ToString(CultureInfo.InvariantCulture));
                entry.SubItems.Add(song.GetGender(true).Replace("Masc.","M").Replace("Fem.", "F"));
                entry.SubItems.Add(song.Length == 0 ? (song.PreviewEnd == 0 ? "0:00" : Parser.GetSongDuration((song.PreviewEnd).ToString(CultureInfo.InvariantCulture))) : Parser.GetSongDuration(song.Length.ToString(CultureInfo.InvariantCulture)));
                entry.SubItems.Add(song.GetRating());
                entry.SubItems.Add(song.GetSource());
                entry.Tag = counter;
                entry.SubItems.Add(DoDifficulty(song.GuitarDiff));
                entry.SubItems.Add(DoDifficulty(song.BassDiff));
                entry.SubItems.Add(DoDifficulty(song.DrumsDiff));
                entry.SubItems.Add(DoDifficulty(song.VocalsDiff));
                entry.SubItems.Add(!modeRB4.Checked ? DoDifficulty(song.KeysDiff) : "N/A");
                entry.SubItems.Add(DoDifficulty(song.BandDiff));
                entry.SubItems.Add(modeRB3.Checked ? DoDifficulty(song.ProGuitarDiff) : "N/A");
                entry.SubItems.Add(modeRB3.Checked ? DoDifficulty(song.ProBassDiff) : "N/A");
                entry.SubItems.Add(modeRB3.Checked ? DoDifficulty(song.ProKeysDiff) : "N/A");
                entry.SubItems.Add(song.SongLink);
                lstSongs.Items.Add(entry);
            }
            UpdateSongCounter();
            SortSongs();
            duplicates.Clear();
            isSearchingForDups = false;
            picWorking.Visible = false;
            return true;
        }

        private string DoDifficulty(int difficulty)
        {
            if (useTierNames.Checked)
            {
                var song = new SongData();
                return song.GetDifficulty(difficulty);
            }
            if (!useTierDots.Checked) return difficulty.ToString(CultureInfo.InvariantCulture);
            switch (difficulty)
            {
                case 0:
                    return "xxxxx";
                case 1:
                    return "ooooo";
                case 2:
                    return "Ooooo";
                case 3:
                    return "OOooo";
                case 4:
                    return "OOOoo";
                case 5:
                    return "OOOOo";
                case 6:
                    return "OOOOO";
                case 7:
                    return "●●●●●";
                default:
                    return "xxxxx";
            }
        }

        private void UpdateSongCounter()
        {
            var count = lstSongs.Items.Count;
            lblCount.Text = count.ToString(CultureInfo.InvariantCulture);
            viewSetlistDetails.Enabled = lstSongs.Items.Count > 0;
            var max = 952;
            string message;
            //don't know limits for RB4, so a more generic message instead
            if (modeRB4.Checked)
            {
                lblCount.ForeColor = Color.LimeGreen;
                toolTip1.SetToolTip(lblCount, "Total number of songs");
                return;
            }
            if (ActiveConsole == "Xbox 360" || ActiveConsole == "PS3")
            {
                count -= 2000;
                max += 2000;
            }
            if (count < 900)
            {
                lblCount.ForeColor = Color.LimeGreen;
                message = "safely under";
            }
            else if (count >= 900 && count <= 952)
            {
                lblCount.ForeColor = Color.Orange;
                message = count == 952 ? "at" : "dangerously close to";
            }
            else
            {
                lblCount.ForeColor = Color.Red;
                message = "over";
            }
            toolTip1.SetToolTip(lblCount, "You are " + message + " the limit of " + max + " total songs for the " + ActiveConsole);
        }

        private void lstSongs_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column != ActiveSortColumn)
            {
                ListSorting = SortOrder.Ascending;
            }
            else
            {
                ListSorting = ListSorting == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            }
            ActiveSortColumn = e.Column;
            SortSongs();
        }

        private void SortSongs()
        {
            lstSongs.ListViewItemSorter = new ListViewItemComparer(lstSongs, ActiveSortColumn, ListSorting);
            lstSongs.Sort();
            GroupByColors();
        }

        private void GroupByColors()
        {
            string col_name;
            //if column was removed, this will throw an error
            try
            {
                col_name = lstSongs.Columns[ActiveSortColumn].Text;
            }
            catch (Exception)
            {
                return;
            }
            if (col_name == "Artist" || col_name == "Album")
            {
                var name = "";
                var light = true;
                for (var i = 0; i < lstSongs.Items.Count; i++)
                {
                    var newname = lstSongs.Items[i].SubItems[ActiveSortColumn].Text.ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = newname;
                    }
                    if (newname != name)
                    {
                        light = !light;
                    }
                    name = newname;
                    if (string.IsNullOrWhiteSpace(name) || (col_name == "Artist" && !alternateColorsForArtists.Checked) || (col_name == "Album" && !alternateColorsForAlbums.Checked))
                    {
                        lstSongs.Items[i].BackColor = lstSongs.BackColor;
                    }
                    else
                    {
                        lstSongs.Items[i].BackColor = light ? AlternateColor : lstSongs.BackColor;
                    }
                }
            }
            else
            {
                for (var i = 0; i < lstSongs.Items.Count; i++)
                {
                    lstSongs.Items[i].BackColor = lstSongs.BackColor;
                }
            }
        }

        private void FiltersChanged(object sender, EventArgs e)
        {
            if (wait || reset || Songs.Count == 0) return;
            if (unSaved)
            {
                if (MessageBox.Show("You have unsaved changes to the song currently selected\nAre you sure you want to do that?\n\nClick NO to cancel and save the changes",
                        AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    return;
                }
            }
            IsUnSaved(false);
            findUnsupported = false;
            findPossDup = false;
            findExactDup = false;
            LoadSongs();
        }

        private void btnSourceAll_Click(object sender, EventArgs e)
        {
            FilterSource(true);
        }

        private void FilterSource(bool enabled)
        {
            wait = true; //don't start sorting things
            chkRB1.Checked = enabled;
            chkRB2.Checked = enabled;
            chkDLC.Checked = enabled;
            chkRBN1.Checked = modeRB3.Checked && enabled;
            chkRBN2.Checked = modeRB3.Checked && enabled;
            chkGDRB.Checked = enabled;
            chkLego.Checked = enabled;
            chkACDC.Checked = enabled;
            chkBlitz.Checked = enabled;
            chkRB4.Checked = modeRB4.Checked && enabled;
            chkRB3.Checked = enabled;
            wait = false;
            FiltersChanged(null, null);
        }

        private void FilterDecades(bool enabled)
        {
            wait = true; //don't start sorting things
            chkYearEarlier.Checked = enabled;
            chkYear50.Checked = enabled;
            chkYear60.Checked = enabled;
            chkYear70.Checked = enabled;
            chkYear80.Checked = enabled;
            chkYear90.Checked = enabled;
            chkYear00.Checked = enabled;
            chkYear10.Checked = enabled;
            wait = false;
            FiltersChanged(null, null);
        }

        private void btnSourceNone_Click(object sender, EventArgs e)
        {
            FilterSource(false);
        }

        private void btnYearAll_Click(object sender, EventArgs e)
        {
            FilterDecades(true);
        }

        private void btnYearNone_Click(object sender, EventArgs e)
        {
            FilterDecades(false);
        }

        private void alternateColorsForArtists_Click(object sender, EventArgs e)
        {
            GroupByColors();
        }
        
        private void numTime_ValueChanged(object sender, EventArgs e)
        {
            if (cboTime.SelectedIndex > 0)
            {
                FiltersChanged(null, null);
            }
        }

        private void PanelsMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            lstSongs.Cursor = Cursors.Default;
            Cursor = Cursors.Default;
            if (!userCanMovePanels.Checked) return;
            var panel = ((Control)sender);
            panel.Cursor = Cursors.NoMove2D;
            mouseX = MousePosition.X;
            mouseY = MousePosition.Y;
            lineLeft.BringToFront();
            lineRight.BringToFront();
            lineTop.BringToFront();
            lineBottom.BringToFront();
            ResizeHelperLines(panel);
            ActivateHelperLines(true);
        }

        private void PanelsMouseUp(object sender, MouseEventArgs e)
        {
            ((Control) sender).Cursor = Cursors.Default;
            ActivateHelperLines(false);
            picWorking.Top = (lstSongs.Height - picWorking.Height) / 2;
        }

        private void PanelsMouseMove(object sender, MouseEventArgs e)
        {
            if (!userCanMovePanels.Checked) return;
            var panel = ((Control)sender);
            if (lstSongs.Cursor == Cursors.SizeAll) //stretching lstSongs
            {
                if (MousePosition.Y > mouseY)
                {
                    lstSongs.Height = lstSongs.Height + (MousePosition.Y - mouseY);
                }
                else if (MousePosition.Y < mouseY && lstSongs.Height > 69)
                {
                    lstSongs.Height = lstSongs.Height - (mouseY - MousePosition.Y);
                }
                mouseY = MousePosition.Y;
                if (MousePosition.X > mouseX)
                {
                    lstSongs.Width = lstSongs.Width + (MousePosition.X - mouseX);
                }
                else if (MousePosition.X < mouseX && lstSongs.Width > 69)
                {
                    lstSongs.Width = lstSongs.Width - (mouseX - MousePosition.X);
                }
                mouseX = MousePosition.X;
            }
            if (panel.Cursor != Cursors.NoMove2D) return;
            if (MousePosition.X != mouseX)
            {
                if (MousePosition.X > mouseX)
                {
                    panel.Left = panel.Left + (MousePosition.X - mouseX);
                }
                else if (MousePosition.X < mouseX)
                {
                    panel.Left = panel.Left - (mouseX - MousePosition.X);
                }
                mouseX = MousePosition.X;
            }
            if (MousePosition.Y == mouseY)
            {
                MoveHelperLines(panel);
                return;
            }
            if (MousePosition.Y > mouseY)
            {
                panel.Top = panel.Top + (MousePosition.Y - mouseY);
            }
            else if (MousePosition.Y < mouseY)
            {
                panel.Top = panel.Top - (mouseY - MousePosition.Y);
            }
            mouseY = MousePosition.Y;
            mouseX = MousePosition.X;
            MoveHelperLines(panel);
        }

        private void ActivateHelperLines(bool visible)
        {
            lineLeft.Visible = visible;
            lineRight.Visible = visible;
            lineTop.Visible = visible;
            lineBottom.Visible = visible;
        }

        private void MoveHelperLines(Control panel)
        {
            lineTop.Left = panel.Left - 25;
            lineBottom.Left = lineTop.Left;
            lineTop.Top = panel.Top;
            lineBottom.Top = panel.Top + panel.Height - 1;
            lineLeft.Top = panel.Top - 25;
            lineRight.Top = lineLeft.Top;
            lineLeft.Left = panel.Left;
            lineRight.Left = panel.Left + panel.Width - 1;
        }

        private void ResizeHelperLines(Control panel)
        {
            lineTop.Width = panel.Width + 50;
            lineBottom.Width = lineTop.Width;
            lineLeft.Height = panel.Height + 50;
            lineRight.Height = lineLeft.Height;
        }

        private void btnInstAll_Click(object sender, EventArgs e)
        {
            FilterInstruments(true);
        }

        private void FilterInstruments(bool enabled)
        {
            wait = true; //don't start sorting things
            chkGuitar.Checked = enabled;
            chkBass.Checked = enabled;
            chkDrums.Checked = enabled;
            chkVocals.Checked = enabled;
            chkHarm2.Checked = enabled;
            chkHarm3.Checked = enabled;
            if (!modeRB4.Checked)
            {
                chkKeys.Checked = enabled;
                chk2X.Checked = enabled;
            }
            if (modeRB3.Checked)
            {
                chkProGuitar.Checked = enabled;
                chkProBass.Checked = enabled;
                chkProKeys.Checked = enabled;
            }
            wait = false;
            FiltersChanged(null, null);
        }

        private void btnInstNone_Click(object sender, EventArgs e)
        {
            FilterInstruments(false);
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            txtSearch.Text = "";
            FiltersChanged(sender,e);
        }
        
        private void resetAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (var panel in FormPanels)
            {
                panel.Visible = true;
                panel.BackColor = Color.Transparent;
                panel.ForeColor = Color.Black;
                ColorPanelText(panel,Color.Black);
            }
            PanelSource.Location = new Point(9,6);
            PanelDecades.Location = new Point(415,6);
            PanelCount.Location = new Point(695,6);
            PanelMaster.Location = new Point(9,82);
            PanelRating.Location = new Point(119,82);
            PanelGenre.Location = new Point(305,82);
            PanelGender.Location= new Point(482,82);
            PanelDuration.Location = new Point(618, 82);
            PanelSearch.Location = new Point(9,132);
            PanelInstruments.Location = new Point(382,132);
            PanelInfo.Location = new Point(9, 467);
            lstSongs.Location = new Point(9,208);
            lstSongs.Height = 253;
            lstSongs.Width = 790;
            lstSongs.BackColor = Color.White;
            lstSongs.ForeColor = Color.Black;
            AlternateColor = Color.AliceBlue;
            GroupByColors();
        }

        private void hideToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var panel = (Panel)MenuSource(sender, true);
            panel.Visible = false;
        }

        private static Control MenuSource(object sender, bool toolstrip)
        {
            ContextMenuStrip owner;
            if (toolstrip)
            {
                // Try to cast the sender to a ToolStripItem
                var menuItem = sender as ToolStripItem;

                // Retrieve the ContextMenuStrip that owns this ToolStripItem
                owner = menuItem.Owner as ContextMenuStrip;
            }
            else
            {
                // Retrieve the ContextMenuStrip that called the function
                owner = sender as ContextMenuStrip;
            }
            // Get the control that is displaying this context menu
            return owner.SourceControl;
        }

        private void toggleSourceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PanelSource.Visible = !PanelSource.Visible;
        }

        private void toggleDecadesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PanelDecades.Visible = !PanelDecades.Visible;
        }

        private void toggleSongCountToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PanelCount.Visible = !PanelCount.Visible;
        }

        private void toggleMasterToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PanelMaster.Visible = !PanelMaster.Visible;
        }

        private void toggleRatingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PanelRating.Visible = !PanelRating.Visible;
        }

        private void toggleGenreToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PanelGenre.Visible = !PanelGenre.Visible;
        }

        private void toggleGenderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PanelGender.Visible = !PanelGender.Visible;
        }

        private void toggleDurationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PanelDuration.Visible = !PanelDuration.Visible;
        }

        private void toggleArtistToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PanelSearch.Visible = !PanelSearch.Visible;
        }
        
        private void toggleInstrumentsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PanelInstruments.Visible = !PanelInstruments.Visible;
        }
        
        private void resetAllFiltersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DoFilterReset();
            FiltersChanged(sender, e);
        }

        private void DoFilterReset()
        {
            wait = true;
            reset = true;
            findPossDup = false;
            findExactDup = false;
            findUnsupported = false;
            findDoNotExport = false;
            FilterSource(true);
            FilterDecades(true);
            chkMasterNo.Checked = true;
            chkMasterYes.Checked = true;
            chkRatingFF.Checked = true;
            chkRatingSR.Checked = true;
            chkRatingM.Checked = true;
            chkRatingNR.Checked = true;
            cboGenre.SelectedIndex = 0;
            chkMale.Checked = true;
            chkFemale.Checked = true;
            txtSearch.Text = "";
            FilterInstruments(false);
            cboTime.SelectedIndex = 0;
            radioAny.Checked = true;
            radioArtist.Checked = false;
            radioSong.Checked = false;
            radioAlbum.Checked = false;
            reset = false;
            wait = false;
        }

        private void ClearDetailedInfo()
        {
            wait = true;
            txtInfoArtist.Text = "";
            txtInfoAlbum.Text = "";
            txtInfoSong.Text = "";
            chkMaster.Checked = false;
            cboGender.SelectedIndex = -1;
            cboInfoGenre.SelectedIndex = -1;
            numReleased.Value = 0;
            numRecorded.Value = 0;
            numTrack.Value = 1;
            txtDuration.Text = "";
            cboRating.SelectedIndex = -1;
            cboVocals.SelectedIndex = -1;
            cboSource.SelectedIndex = -1;
            diffGuitar.Tag = 0;
            diffBass.Tag = 0;
            diffDrums.Tag = 0;
            diffVocals.Tag = 0;
            diffKeys.Tag = 0;
            diffBand.Tag = 0;
            diffProGuitar.Tag = 0;
            diffProBass.Tag = 0;
            diffProKeys.Tag = 0;
            cboVocals.Tag = 0;
            ClearDiffBoxes(false);
            isNewSong = true;
            wait = false;
        }

        private void ShowDetailedInfo(int index)
        {
            wait = true;
            isNewSong = false;
            txtInfoArtist.Text = Songs[index].Artist;
            txtInfoSong.Text = Songs[index].Name;
            txtInfoAlbum.Text = Songs[index].Album;
            chkMaster.Checked = Songs[index].Master;
            switch (Songs[index].GetGender())
            {
                case "Male":
                    cboGender.SelectedIndex = 0;
                    break;
                case "Female":
                    cboGender.SelectedIndex = 1;
                    break;
                case "N/A":
                    cboGender.SelectedIndex = 2;
                    break;
            }
            numRecorded.Value = Songs[index].YearRecorded > 0 ? Songs[index].YearRecorded : Songs[index].YearReleased;
            numReleased.Value = Songs[index].YearReleased;
            cboRating.SelectedIndex = Songs[index].Rating - 1;
            cboVocals.SelectedIndex = Songs[index].VocalsDiff > 0 ? Songs[index].VocalParts : 0;
            numTrack.Value = Songs[index].TrackNumber == 0 ? 1 : Songs[index].TrackNumber;
            if (string.IsNullOrWhiteSpace(Songs[index].Genre))
            {
                cboInfoGenre.SelectedIndex = -1;
            }
            else
            {
                for (var i = 0; i < cboInfoGenre.Items.Count; i++)
                {
                    if (cboInfoGenre.Items[i].ToString() != Songs[index].Genre) continue;
                    cboInfoGenre.SelectedIndex = i;
                    break;
                }
            }
            for (var i = 0; i < cboSource.Items.Count; i++)
            {
                if (cboSource.Items[i].ToString() != Songs[index].GetSource()) continue;
                cboSource.SelectedIndex = i;
                break;
            }
            txtDuration.Text = Parser.GetSongDuration(Songs[index].Length.ToString(CultureInfo.InvariantCulture));
            try
            {
                SetDifficulty(diffDrums, Songs[index].DrumsDiff);
                SetDifficulty(diffBass, Songs[index].BassDiff);
                SetDifficulty(diffGuitar, Songs[index].GuitarDiff);
                SetDifficulty(diffVocals, Songs[index].VocalsDiff);
                SetDifficulty(diffBand, Songs[index].BandDiff);
                if (!modeRB4.Checked)
                {
                    SetDifficulty(diffKeys, Songs[index].KeysDiff);
                }
                if (modeRB3.Checked)
                {
                    SetDifficulty(diffProGuitar, Songs[index].ProGuitarDiff);
                    SetDifficulty(diffProBass, Songs[index].ProBassDiff);
                    SetDifficulty(diffProKeys, Songs[index].ProKeysDiff);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error displaying instrument difficulties:\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            wait = false;
        }

        private void SetDifficulty(PictureBox instrument, int difficulty)
        {
            switch (difficulty)
            {
                case 1:
                    instrument.Image = RESOURCE_DIFF_0;
                    break;
                case 2:
                    instrument.Image = RESOURCE_DIFF_1;
                    break;
                case 3:
                    instrument.Image = RESOURCE_DIFF_2;
                    break;
                case 4:
                    instrument.Image = RESOURCE_DIFF_3;
                    break;
                case 5:
                    instrument.Image = RESOURCE_DIFF_4;
                    break;
                case 6:
                    instrument.Image = RESOURCE_DIFF_5;
                    break;
                case 7:
                    instrument.Image = RESOURCE_DIFF_6;
                    break;
                default:
                    instrument.Image = RESOURCE_DIFF_NOPART;
                    break;
            }
            toolTip1.SetToolTip(instrument, new SongData().GetDifficulty(difficulty));
            instrument.Tag = difficulty;
            if (instrument == diffVocals && difficulty > 0)
            {
                cboVocals.Tag = difficulty;
            }
        }

        private void lstSongs_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstSongs.SelectedItems.Count == 0)
            {
                btnDeleteSong.Enabled = false;
                return;
            }
            if (wait)
            {
                btnSave.Visible = false;
                btnDeleteSong.Enabled = false;
                return;
            }
            if (unSaved)
            {
                if (MessageBox.Show("You have unsaved changes to the song currently selected!\nAre you sure you want to select a new song and lose those changes?\n\nClick NO to cancel and go back...",
                        AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    return;
                }
                btnSave.Visible = false;
            }
            IsUnSaved(false);
            
            if (lstSongs.SelectedItems.Count > 1)
            {
                EnableDisableInfo(false);
                ClearDetailedInfo();
                ClearDiffBoxes(false);
                btnDeleteSong.Enabled = true;
            }
            else //only one item left selected, show the info
            {
                try
                {
                    var index = Convert.ToInt16(lstSongs.SelectedItems[0].Tag);
                    if (index <= -1) return;
                    EnableDisableInfo(true);
                    ClearDetailedInfo();
                    ClearDiffBoxes(true);
                    cboVocals.Tag = 0;
                    ShowDetailedInfo(index);
                    btnDeleteSong.Enabled = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("There was an error accessing that song's information\nThe error says: " + ex.Message, AppName,
                        MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    wait = false; //otherwise nothing else refreshes
                }
            }
        }

        private void toggleInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            PanelInfo.Visible = !PanelInfo.Visible;
        }

        private void btnNew_Click(object sender, EventArgs e)
        {
            if (isLocked)
            {
                DoUnlockWarning();
                return;
            }
            PrepForNewSong();
        }

        private void PrepForNewSong()
        {
            if (unSaved)
            {
                if (MessageBox.Show("You have unsaved changes to the song currently selected\nAre you sure you want to add a new song and lose those changes?\n\nClick NO to cancel and save the changes",
                        AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    return;
                }
            }
            EnableDisableInfo(true);
            ClearDetailedInfo();
            btnDeleteSong.Enabled = false;
            btnSave.Visible = false;
            wait = true;
            ClearDiffBoxes(true);
            cboRating.SelectedIndex = 3;//NR
            cboGender.SelectedIndex = 2;//N/A
            chkMaster.Checked = true;
            cboInfoGenre.SelectedIndex = cboInfoGenre.Items.IndexOf("Rock");
            cboSource.SelectedIndex = modeRB4.Checked ? cboSource.Items.IndexOf("RB4 DLC") : cboSource.Items.Count - 1;
            cboVocals.SelectedIndex = 0;
            IsUnSaved(false);
            isNewSong = true;
            txtInfoArtist.Focus();
            wait = false;
        }

        private void InfoChanged(object sender, EventArgs e)
        {
            if (wait) return;
            btnSave.Visible = true;
            IsUnSaved(true);
        }

        private void IsUnSaved(bool unsaved)
        {
            unSaved = unsaved;
            if (!unsaved && Text.EndsWith("*", StringComparison.Ordinal))
            {
                Text = Text.Substring(0, Text.Length - 1);
            }
            else if (unsaved)
            {
                Text = Text.EndsWith("*", StringComparison.Ordinal) ? Text : Text + "*";
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (isLocked)
            {
                DoUnlockWarning();
                return;
            }
            if (string.IsNullOrWhiteSpace(txtInfoArtist.Text))
            {
                MessageBox.Show("Enter an artist / band name", AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                txtInfoArtist.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(txtInfoSong.Text))
            {
                MessageBox.Show("Enter a song name", AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                txtInfoSong.Focus();
                return;
            }
            if (!ValidateDuration()) return;
            IsUnSaved(false);
            int index;
            if (isNewSong)
            {
                Songs.Add(new SongData());
                index = Songs.Count - 1;

                //need some sort of value, only do this with new entries, keep old values for existing entries
                Songs[index].ShortName = txtInfoSong.Text.Replace(" ", "").Replace("'", "").ToLowerInvariant();
                Songs[index].SongId = 1234567890;
            }
            else if (lstSongs.FocusedItem != null)
            {
                index = (int)(lstSongs.FocusedItem.Tag);
            }
            else
            {
                //wtf are we here?
                return;
            }
            Songs[index].Name = txtInfoSong.Text;
            Songs[index].Artist = txtInfoArtist.Text;
            Songs[index].Album = txtInfoAlbum.Text;
            Songs[index].VocalParts = cboVocals.SelectedIndex;
            Songs[index].YearRecorded = (int)numRecorded.Value;
            Songs[index].YearReleased = (int)numReleased.Value;
            Songs[index].TrackNumber = (int)numTrack.Value;
            string source;
            switch (cboSource.SelectedIndex)
            {
                    //adding the ## in front of the shortname allows us to bypass the check for RBN1 and RBN2
                case 0:
                    source = "rb1";
                    Songs[index].ShortName = "##" + Songs[index].ShortName.Replace("##", "");
                    break;
                case 1:
                    source = "rb1_dlc";
                    break;
                case 2:
                    source = "rb2";
                    Songs[index].ShortName = "##" + Songs[index].ShortName.Replace("##", "");
                    break;
                case 3:
                    source = "rb2_dlc";
                    break;
                case 4:
                    source = "rb3";
                    break;
                case 5:
                    source = "rb3_dlc";
                    break;
                case 6:
                    source = "rb4";
                    break;
                case 7:
                    source = "rb4_dlc";
                    break;
                case 8:
                    source = "rbn1";
                    break;
                case 9:
                    source = "rbn2";
                    break;
                case 10:
                    source = "lego";
                    break;
                case 11:
                    source = "gdrb";
                    break;
                case 12:
                    source = "acdc";
                    break;
                case 13:
                    source = "blitz";
                    break;
                default:
                    source = modeRB4.Checked ? "rb4_dlc" : "rb3_dlc";
                    break;
            }
            Songs[index].Source = source;
            Songs[index].Rating = cboRating.SelectedIndex + 1;
            Songs[index].Genre = cboInfoGenre.Text;
            Songs[index].Gender = cboGender.SelectedIndex == 0 ? "Male" : (cboGender.SelectedIndex == 1 ? "Female" : "N/A");
            Songs[index].Master = chkMaster.Checked;
            var time = Songs[index].Length;
            try
            {
                var hours = 0;
                int minutes;
                int seconds;
                if (txtDuration.Text.Count(x => x == ':') > 1)
                {
                    var i1 = txtDuration.Text.IndexOf(":", StringComparison.Ordinal);
                    hours = Convert.ToInt16(txtDuration.Text.Substring(0, i1));
                    var i2 = txtDuration.Text.IndexOf(":", i1 + 1, StringComparison.Ordinal);
                    minutes = Convert.ToInt16(txtDuration.Text.Substring(i1 + 1, i2 - (i1 + 1)));
                    seconds = Convert.ToInt16(txtDuration.Text.Substring(i2 + 1, txtDuration.Text.Length - (i2 + 1)));
                }
                else
                {
                    var i = txtDuration.Text.IndexOf(":", StringComparison.Ordinal);
                    minutes = Convert.ToInt16(txtDuration.Text.Substring(0, i));
                    seconds = Convert.ToInt16(txtDuration.Text.Substring(i + 1, txtDuration.Text.Length - (i + 1)));
                }
                Songs[index].Length = (((hours * 60) + minutes) * 60000) + (seconds * 1000);
            }
            catch (Exception)
            {
                Songs[index].Length = time;
            }
            Songs[index].DrumsDiff = Tools.GetDiffTag(diffDrums);
            Songs[index].BassDiff = Tools.GetDiffTag(diffBass);
            Songs[index].ProBassDiff = Tools.GetDiffTag(diffProBass);
            Songs[index].GuitarDiff = Tools.GetDiffTag(diffGuitar);
            Songs[index].ProGuitarDiff = Tools.GetDiffTag(diffProGuitar);
            Songs[index].KeysDiff = Tools.GetDiffTag(diffKeys);
            Songs[index].ProKeysDiff = Tools.GetDiffTag(diffProKeys);
            Songs[index].VocalsDiff = Tools.GetDiffTag(diffVocals);
            Songs[index].BandDiff = Tools.GetDiffTag(diffBand);
            ClearDetailedInfo();
            SaveSetlist(ActiveSetlistPath);
            LoadSongs();
        }
        
        private void EnableDisableInfo(bool enabled)
        {
            foreach (var c in PanelInfo.Controls)
            {
                ((Control) c).Enabled = enabled;
            }
            ActiveMarker.Visible = false;
        }

        private void renameSetlistToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (isLocked)
            {
                DoUnlockWarning();
                return;
            }
            var oldName = ActiveSetlist;
            var fileName = ActiveSetlistPath;
            var popup = new PasswordUnlocker(oldName);
            popup.Renamer(); //change settings for renaming
            popup.ShowDialog();
            var newName = popup.EnteredText;
            popup.Dispose();
            if (newName == oldName || string.IsNullOrWhiteSpace(newName))
            {
                return;
            }
            ActiveSetlist = newName;
            var setlist = File.ReadAllLines(fileName);
            setlist[0] = "SetlistName=" + newName;
            var sw = new StreamWriter(fileName, false, System.Text.Encoding.UTF8);
            foreach (var line in setlist)
            {
                sw.WriteLine(line);
            }
            sw.Dispose();
            tabHolder.TabPages[0].Text = ActiveSetlist;
            Text = "Setlist Manager - " + ActiveSetlist;
            newName = Path.GetDirectoryName(ActiveSetlistPath) + "\\" + Tools.CleanString(newName, true).Replace("'", "").Replace(" ", "") +".setlist";
            if (!File.Exists(newName))
            {
                try
                {
                    File.Copy(ActiveSetlistPath, newName);
                }
                catch (Exception)
                {
                    return;
                }
                Tools.DeleteFile(ActiveSetlistPath);
                ActiveSetlistPath = newName;
            }
            SaveOptions();
        }

        private void lblCount_MouseDown(object sender, MouseEventArgs e)
        {
            lblCount.Cursor = Cursors.NoMove2D;
            PanelCount.Cursor = Cursors.NoMove2D;
            mouseX = MousePosition.X;
            mouseY = MousePosition.Y;
            ResizeHelperLines(PanelCount);
        }

        private void lblCount_MouseMove(object sender, MouseEventArgs e)
        {
            PanelsMouseMove(PanelCount, e);
        }

        private void lblCount_MouseUp(object sender, MouseEventArgs e)
        {
            lblCount.Cursor = Cursors.Default;
            PanelCount.Cursor = Cursors.Default;
            ActivateHelperLines(false);
        }

        private void sendToBackToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var panel = MenuSource(sender, true);
            panel.SendToBack();
        }

        private void bringToFrontToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var panel = MenuSource(sender, true);
            panel.BringToFront();
            picWorking.BringToFront();
        }

        private void contextMenuStrip1_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var panel = MenuSource(sender, false);
            hideToolStripMenuItem.Visible = panel != lstSongs;
            alternateColor.Visible = panel == lstSongs;
            editColumnsToolStrip.Visible = panel == lstSongs;
            resizeToolStripMenuItem.Visible = panel == lstSongs;
            resizeToolStripMenuItem.Enabled = userCanMovePanels.Checked;
            var visible = panel == lstSongs && lstSongs.SelectedItems.Count > 0;
            dONOTPRINT.Visible = visible;
            deleteSelectedToolStrip.Visible = visible;
            contextSeparator1.Visible = visible;
            visible = panel == lstSongs && lstSongs.SelectedItems.Count == 1;
            openLinkInBrowser.Visible = visible;
            editLinkToSong.Visible = visible;
            contextSeparator0.Visible = visible;
            if (panel != lstSongs || lstSongs.SelectedItems.Count != 1) return;
            var index = Convert.ToInt16(lstSongs.SelectedItems[0].Tag);
            if (index == -1) return;
            dONOTPRINT.Checked = Songs[index].DoNotExport;
            openLinkInBrowser.Enabled = !string.IsNullOrWhiteSpace(Songs[index].SongLink);
        }

        private void contextMenuStrip2_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            renameSetlistToolStripMenuItem.Enabled = !string.IsNullOrWhiteSpace(ActiveSetlist) && File.Exists(ActiveSetlistPath) && !isLocked;
        }

        private void btnDeleteSong_Click(object sender, EventArgs e)
        {
            DeleteSelected();
        }

        private static void DoUnlockWarning()
        {
            MessageBox.Show("You must unlock the program first", "Locked", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }

        private void DeleteSelected()
        {
            if (isLocked)
            {
                DoUnlockWarning();
                return;
            }
            if (lstSongs.SelectedItems.Count == 0) return;
            var indices = (from object item in lstSongs.SelectedItems select Convert.ToInt16(((ListViewItem)item).Tag)).Select(dummy => (int)dummy).ToList();
            var to_delete = indices.Aggregate("", (current, index) => current + Songs[index].Artist + " - " + Songs[index].Name + "\n");
            if (confirmBeforeDeleting.Checked)
            {
                if (MessageBox.Show("Are you sure you want to delete " + (indices.Count() > 1 ? "these " + indices.Count() + " songs" : "this song") + "?\n\n" +
                    (indices.Count > 20 ? "(too many songs selected to display)\n" : to_delete) + "\nTHIS CANNOT BE UNDONE", AppName,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    return;
                }
            }
            ClearDetailedInfo();
            indices.Sort();
            for (var i = indices.Count - 1; i >= 0; i--)
            {
                Songs.RemoveAt(indices[i]);
            }
            SaveSetlist(ActiveSetlistPath);
            LoadSongs();
        }

        private Color ColorPicker(Color initialcolor)
        {
            colorDialog1.Color = initialcolor;
            colorDialog1.SolidColorOnly = true;
            colorDialog1.ShowDialog();
            return colorDialog1.Color;
        }

        private void backgroundColor_Click(object sender, EventArgs e)
        {
            var panel = MenuSource(sender, true);
            panel.BackColor = ColorPicker(panel.BackColor);
            if (panel.Name != lstSongs.Name) return;
            GroupByColors();
        }

        private void textColor_Click(object sender, EventArgs e)
        {
            var panel = MenuSource(sender, true);
            panel.ForeColor = ColorPicker(panel.ForeColor);
            ColorPanelText(panel,panel.ForeColor);
        }

        private void ColorPanelText(Control panel, Color color)
        {
            foreach (var c in (from object c in panel.Controls where !(c is Button) select c).Where(c => c != lblCount))
            {
                ((Control)c).ForeColor = color;
            }
        }

        private void alternateColor_Click(object sender, EventArgs e)
        {
            AlternateColor = ColorPicker(AlternateColor);
            GroupByColors();
        }

        private void resizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!userCanMovePanels.Checked)
            {
                MessageBox.Show("Click on 'Panels -> User can modify panels' first", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            lstSongs.Cursor = Cursors.SizeAll;
            Cursor = Cursors.SizeAll;
            mouseY = MousePosition.Y;
            mouseX = MousePosition.X;
        }

        private void lstSongs_KeyUp(object sender, KeyEventArgs e)
        {
            if (lstSongs.Items.Count > 0)
            {
                if (e.Control && e.KeyCode == Keys.A)
                {
                    for (var i = 0; i < lstSongs.Items.Count; i++)
                    {
                        lstSongs.Items[i].Selected = true;
                    }
                    return;
                }
                if (e.Control && e.KeyCode == Keys.D)
                {
                    for (var i = 0; i < lstSongs.Items.Count; i++)
                    {
                        lstSongs.Items[i].Selected = false;
                    }
                    lstSongs.Items[0].Selected = true;
                    lstSongs.Items[0].EnsureVisible();
                    return;
                }
                if (e.KeyCode == Keys.Delete)
                {
                    DeleteSelected();
                    return;
                }
            }
            if (e.KeyCode != Keys.Escape || lstSongs.Cursor != Cursors.SizeNS) return;
            lstSongs.Cursor = Cursors.Default;
            Cursor = Cursors.Default;
        }

        private void tabSetlist_MouseDown(object sender, MouseEventArgs e)
        {
            lstSongs.Cursor = Cursors.Default;
            Cursor = Cursors.Default;
        }
        
        private void findDuplicatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            findPossDup = true;
            findExactDup = false;
            findUnsupported = false;
            findDoNotExport = false;
            LoadSongs();
        }

        private void FindDuplicates()
        {
            var poss = findPossDup;
            var exact = findExactDup;
            isSearchingForDups = true;
            var songs = Songs.Select(t => CleanName(t.Name, true).ToLowerInvariant()).ToList();
            var artists = Songs.Select(t => CleanName(t.Artist, true).ToLowerInvariant()).ToList();
            for (var i = 0; i < Songs.Count; i++)
            {
                var name = CleanName(Songs[i].Name, true).ToLowerInvariant();
                var artist = CleanName(Songs[i].Artist, true).ToLowerInvariant();
                if (!songs.Contains(name)) continue;
                if (findExactDup && artists[songs.IndexOf(name)] != artist) continue;
                if (songs.IndexOf(name) == i) continue;
                if (!duplicates.Contains(i))
                {
                    duplicates.Add(i);
                }
                if (!duplicates.Contains(songs.IndexOf(name)))
                {
                    duplicates.Add(songs.IndexOf(name));
                }
            }
            resetAllFilters.PerformClick();
            ListSorting = SortOrder.Ascending;
            //find "Song" column (since user can move it around)
            for (var i = lstSongs.Columns.Count - 1; i > 0; i--)
            {
                if (lstSongs.Columns[i].Text != "Song") continue;
                ActiveSortColumn = i;
                break;
            }
            SortSongs();
            //these get turned off by resetAllFilters, return value until manually turned off
            findExactDup = exact;
            findPossDup = poss;
        }

        private void findExactDuplicatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            findPossDup = false;
            findExactDup = true;
            findUnsupported = false;
            findDoNotExport = false;
            LoadSongs();
        }
        
        private void normalizeFeaturedArtists_Click(object sender, EventArgs e)
        {
            if (normalizeFeaturedArtists.Checked)
            {
                removeFeaturedArtists.Checked = false;
            }
            FiltersChanged(sender, e);
        }

        private void moveFeaturedArtistsToSongName_Click(object sender, EventArgs e)
        {
            if (moveFeaturedArtistsToSongName.Checked)
            {
                removeFeaturedArtists.Checked = false;
            }
            FiltersChanged(sender, e);
        }

        private void removeFeaturedArtists_Click(object sender, EventArgs e)
        {
            if (removeFeaturedArtists.Checked)
            {
                moveFeaturedArtistsToSongName.Checked = false;
                normalizeFeaturedArtists.Checked = false;
            }
            FiltersChanged(sender, e);
        }
        
        private void findUnsupportedCharacters_Click(object sender, EventArgs e)
        {
            findUnsupported = true;
            findPossDup = false;
            findExactDup = false;
            findDoNotExport = false;
            LoadSongs();
        }

        private void cboVocals_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (wait) return;
            cboGender.SelectedIndex = cboVocals.SelectedIndex == 0 ? 2 : 0; //N/A if no vocals, default to male if yes
            if (cboVocals.SelectedIndex == 0)
            {
                SetDifficulty(diffVocals, 0);
            }
            else if (diffVocals.Image == RESOURCE_DIFF_NOPART)
            {
                SetDifficulty(diffVocals, Convert.ToInt16(cboVocals.Tag));
            }
            InfoChanged(sender, e);
        }
        
        private void lockToolStrip_Click(object sender, EventArgs e)
        {
            var password = new PasswordUnlocker();
            password.ShowDialog();
            var pass = password.EnteredText;
            if (string.IsNullOrWhiteSpace(pass) || string.IsNullOrWhiteSpace(pass)) return;
            if (!isLocked)
            {
                LockPass = pass;
                lockToolStrip.Text = "Unlock";
                isLocked = true;
            }
            else
            {
                if (LockPass != pass) return;
                lockToolStrip.Text = "Lock";
                isLocked = false;
            }
        }

        private void tmrHighlight_Tick(object sender, EventArgs e)
        {
            lockToolStrip.BackColor = menuStrip1.BackColor;
            tmrHighlight.Enabled = false;
        }

        private void dONOTPRINT_Click(object sender, EventArgs e)
        {
            if (isLocked)
            {
                DoUnlockWarning();
                return;
            }
            foreach (var index in lstSongs.SelectedItems.Cast<ListViewItem>().Select(item => Convert.ToInt16(item.Tag)).Where(index => index != -1))
            {
                Songs[index].DoNotExport = dONOTPRINT.Checked;
            }
            SaveSetlist(ActiveSetlistPath);
            if (findDoNotExport)
            {
                LoadSongs();
            }
        }

        private void findSongsMarkedDoNotExport_Click(object sender, EventArgs e)
        {
            findDoNotExport = true;
            findPossDup = false;
            findExactDup = false;
            findUnsupported = false;
            LoadSongs();
        }
        
        private void txtArtist_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyData != Keys.Enter || string.IsNullOrWhiteSpace(txtSearch.Text)) return;
            FiltersChanged(sender, e);
        }
        
        private sealed class DarkRenderer : ToolStripProfessionalRenderer
        {
            public DarkRenderer() : base(new DarkColors()) { }
        }

        private sealed class DarkColors : ProfessionalColorTable
        {
            public override Color ImageMarginGradientBegin
            {
                get { return mMenuBackground; }
            }
            public override Color ImageMarginGradientEnd
            {
                get { return mMenuBackground; }
            }
            public override Color ImageMarginGradientMiddle
            {
                get { return mMenuBackground; }
            }
            public override Color ToolStripDropDownBackground
            {
                get { return mMenuBackground; }
            }
        }

        private void picPin_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            switch (picPin.Tag.ToString())
            {
                case "pinned":
                    picPin.Image = Resources.unpinned;
                    picPin.Tag = "unpinned";
                    break;
                case "unpinned":
                    picPin.Image = Resources.pinned;
                    picPin.Tag = "pinned";
                    break;
            }
            TopMost = picPin.Tag.ToString() == "pinned";
        }

        private void findAndLabelBlitzSongs_Click(object sender, EventArgs e)
        {
            if (isLocked)
            {
                DoUnlockWarning();
                return;
            }
            FilterSource(false);
            chkBlitz.Checked = true;
            var blitzIDs = new List<int>
            {
                1010549, 1010524, 1010529, 1010499, 1010528, 
                1010554, 1010522, 1010557, 1010478, 1010530,
                1010494, 1010539, 1010550, 1010508, 1010381,
                1010541, 1010526, 1010551, 1010553, 1010493,
                1010552, 1010492, 1010437, 1010517, 1010378
            };
            foreach (var song in Songs.Where(song => blitzIDs.Contains(song.SongId)))
            {
                song.Source = "blitz";
            }
            SaveSetlist(ActiveSetlistPath);
            LoadSongs();
        }

        private void UpdateGameMode(object sender, EventArgs e)
        {
            if (isLocked)
            {
                DoUnlockWarning();
                return;
            }
            modeRB3.Checked = false;
            modeRB4.Checked = false;
            ((ToolStripMenuItem) sender).Checked = true;
            doUpdateGameMode();
        }

        private void doUpdateGameMode()
        {
            chkKeys.Enabled = modeRB3.Checked;
            chk2X.Enabled = modeRB3.Checked;
            chkProKeys.Enabled = modeRB3.Checked;
            chkProBass.Enabled = modeRB3.Checked;
            chkProGuitar.Enabled = modeRB3.Checked;
            chkRB4.Enabled = modeRB4.Checked;
            chkRBN1.Enabled = modeRB3.Checked;
            chkRBN2.Enabled = modeRB3.Checked;
            correctMetadataForRB4.Enabled = modeRB4.Checked && viewSetlistDetails.Enabled;
            rB4PS4.Enabled = modeRB4.Checked;
            rB4XOne.Enabled = modeRB4.Checked;
            rB3PS3.Enabled = modeRB3.Checked;
            rB3Wii.Enabled = modeRB3.Checked;
            rB3X360.Enabled = modeRB3.Checked;
            EnableDisableInfo(false);
            diffKeys.Cursor = modeRB3.Checked ? Cursors.Hand : Cursors.No;
            diffProKeys.Cursor = modeRB3.Checked ? Cursors.Hand : Cursors.No;
            diffProBass.Cursor = modeRB3.Checked ? Cursors.Hand : Cursors.No;
            diffProGuitar.Cursor = modeRB3.Checked ? Cursors.Hand : Cursors.No;
            rockBand4ToolStripMenuItem.Visible = modeRB4.Checked;
            if (ActiveConsole.Contains("PS"))
            {
                ActiveConsole = modeRB4.Checked ? "PS4" : "PS3";
            }
            else if (ActiveConsole.Contains("Xbox"))
            {
                ActiveConsole = modeRB4.Checked ? "Xbox One" : "Xbox 360";
            }
            picGame.Image = modeRB4.Checked ? Resources.icon_rb4 : Resources.icon_rb3;
            var controls = new List<CheckBox> { chkRB4, chkProBass, chkProGuitar, chkProKeys, chkKeys, chk2X, chkRBN1, chkRBN2 };
            foreach (var control in controls.Where(control => !control.Enabled))
            {
                control.Checked = false;
            }
            UpdateSongCounter();
        }

        private void AddRB4Songs(string file)
        {
            if (isLocked)
            {
                DoUnlockWarning();
                return;
            }
            if (string.IsNullOrWhiteSpace(ActiveSetlist) || !File.Exists(ActiveSetlistPath))
            {
                MessageBox.Show("Create a Setlist first", AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            var path = binFolder + file;
            if (!File.Exists(path))
            {
                MessageBox.Show("Required file '" + path + "' is missing, can't add those songs", AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            var newSongs = GrabSongsFromSetlist(path, false);
            if (!newSongs.Any())
            {
                MessageBox.Show("There was a problem loading those songs!\nNext time you run " + AppName + " I'll re-download that file from the server and you can try again", AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                Tools.DeleteFile(binFolder + file);
                UpdateRB4Features();
                return;
            }
            FinalizeImport(newSongs);
        }

        private void UpdateRB4Features()
        {
            addGameSongs.Enabled = File.Exists(binFolder + "songs_rb4");
            addPreorderSongs.Enabled = File.Exists(binFolder + "songs_rb4_pre");
            addAmazonSongs.Enabled = File.Exists(binFolder + "songs_rb4_amzn");
            addPlayStationPlusSongs.Enabled = File.Exists(binFolder + "songs_rb4_psp");
            addXboxOneSongs.Enabled = File.Exists(binFolder + "songs_rb4_xone");
            addRB1ExportSongs.Visible = File.Exists(binFolder + "songs_rb1");
            addRB2ExportSongs.Visible = File.Exists(binFolder + "songs_rb2");
            addRB3ExportSongs.Visible = File.Exists(binFolder + "songs_rb3");
            addGreenDayExportSongs.Visible = File.Exists(binFolder + "songs_gdrb");
            addLegoExportSongs.Visible = File.Exists(binFolder + "songs_lego");
            addBlitzSongs.Visible = File.Exists(binFolder + "songs_blitz");
            addACDCSongs.Visible = File.Exists(binFolder + "songs_acdc");
        }
        
        private void correctMetadataForRB4_Click(object sender, EventArgs e)
        {
            CorrectMetadata(true);
        }

        private void CorrectMetadata(bool isRB4)
        {
            if (isLocked)
            {
                DoUnlockWarning();
                return;
            }
            var message = "This will strip RB3-specific metadata from the songs and only leave the metadata that " + (isRB4 ? "RB4" : "Blitz") + " uses\n" +
                                   "This CAN NOT be undone\n\nAre you sure you want to do that?";
            if (MessageBox.Show(message, AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            foreach (var song in Songs)
            {
                if (isRB4)
                {
                    song.KeysDiff = 0;
                    song.Name = song.Name.Replace("(RB3 version)", "").Replace("(RB3 Version)", "").Replace("(2x Bass Pedal)", "").Trim();
                }
                song.ProKeysDiff = 0;
                song.ProBassDiff = 0;
                song.ProGuitarDiff = 0;
            }
            SaveSetlist(ActiveSetlistPath);
            LoadSongs();
        }
        
        private void downloader_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            DownloadRB4File("songs_rb4");
            DownloadRB4File("songs_rb4_pre");
            DownloadRB4File("songs_rb4_amzn");
            DownloadRB4File("songs_rb4_psp");
            DownloadRB4File("songs_rb4_xone");
            DownloadRB4File("songs_rb1");
            DownloadRB4File("songs_rb2");
            DownloadRB4File("songs_rb3");
            DownloadRB4File("songs_gdrb");
            DownloadRB4File("songs_lego");
            DownloadRB4File("songs_blitz");
            DownloadRB4File("songs_acdc");
        }

        private void DownloadRB4File(string file)
        {
            if (File.Exists(binFolder + file)) return;
            using (var client = new WebClient())
            {
                try
                {
                    client.DownloadFile("http://www.keepitfishy.com/rb4/setlists/" + file, binFolder + file);
                }
                catch (Exception)
                { }
            }
        }

        private void downloader_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            UpdateRB4Features();
        }
        
        private void deleteSelectedToolStrip_Click(object sender, EventArgs e)
        {
            btnDeleteSong.PerformClick();
        }

        private void txtInfoArtist_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            MoveMarker(sender);
        }

        private void MoveMarker(object sender)
        {
            var control = (Control)sender;
            ActiveMarker.Width = control.Width;
            ActiveMarker.Left = control.Left;
            ActiveMarker.Top = control.Top + control.Height;
            ActiveMarker.Visible = true;
        }

        private void txtInfoSong_Leave(object sender, EventArgs e)
        {
            ((Control)sender).BackColor = Color.WhiteSmoke;
        }
        
        private void PanelInfo_Leave(object sender, EventArgs e)
        {
            ActiveMarker.Visible = false;
        }

        private void txtArtist_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ((Control) sender).BackColor = Color.LightGoldenrodYellow;
        }
        
        private void txtDuration_Leave(object sender, EventArgs e)
        {
            ValidateDuration();
        }

        private bool ValidateDuration()
        {
            try
            {
                Convert.ToDateTime(txtDuration.Text);
                return true;
            }
            catch (Exception)
            {
                MessageBox.Show("That's not a valid duration value", AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                txtDuration.Focus();
                return false;
            }
        }

        private void cboTime_SelectedIndexChanged(object sender, EventArgs e)
        {
            numTime.Enabled = cboTime.SelectedIndex > 0; 
            FiltersChanged(sender, e);
        }
        
        private void ImportDLC(int type)
        {
            if (isLocked)
            {
                DoUnlockWarning();
                return;
            }
            if (string.IsNullOrWhiteSpace(ActiveSetlist) || !File.Exists(ActiveSetlistPath))
            {
                MessageBox.Show("Create a Setlist first", AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            var source = "MyRockBandSongs.com";
            if (type == 1)
            {
                source = "DLC Quickplay";
            }

            var xmlFile = binFolder + "songs.xml";
            Tools.DeleteFile(xmlFile);
            if (type == 0)
            {
                var link = Interaction.InputBox("Enter link to MyRockBandSongs.com XML file.\n\nClick CANCEL to use local XML file.", AppName);
                if (!string.IsNullOrWhiteSpace(link))
                {
                    if (!link.Contains("myrockbandsongs.com") || !link.Contains("print-booklet/xml"))
                    {
                        MessageBox.Show("Invalid link format, try again", AppName, MessageBoxButtons.OK,
                            MessageBoxIcon.Exclamation);
                        return;
                    }
                    picWorking.Visible = true;
                    picWorking.Refresh();
                    using (var client = new WebClient())
                    {
                        try
                        {
                            client.DownloadFile(link, xmlFile);
                        }
                        catch (Exception)
                        {
                        }
                        if (!File.Exists(xmlFile))
                        {
                            MessageBox.Show("Unable to download XML file, make sure you're entering the correct link",
                                AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                            return;
                        }
                    }
                    picWorking.Visible = false;
                }
            }
            if (!File.Exists(xmlFile))
            {
                var ofd = new OpenFileDialog
                {
                    InitialDirectory = Environment.CurrentDirectory,
                    Title = "Select " + source + " XML file",
                    Multiselect = false,
                    Filter = "XML Files(*.xml)|*xml;*txt"
                };
                ofd.ShowDialog();
                if (string.IsNullOrWhiteSpace(ofd.FileName)) return;
                xmlFile = ofd.FileName;
            }

            Environment.CurrentDirectory = Path.GetDirectoryName(xmlFile);
            var ext = Path.GetExtension(xmlFile);
            switch (ext)
            {
                case ".xml":
                case ".txt":
                    ImportXML(xmlFile);
                    break;
                default:
                    MessageBox.Show("That's not a valid file to import", AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
            }
        }

        private static string CleanXMLLine(string line, string field)
        {
            return line.Replace("<" + field + ">", "").Replace("</" + field + ">", "").Replace("<" + field.ToLowerInvariant() + ">", "").Replace("</" + field.ToLowerInvariant() + ">", "").Trim();
        }

        private static int CleanXMLValue(string line, string field, bool isDiff = false)
        {
            int value;
            var input = CleanXMLLine(line, field);
            if (string.IsNullOrWhiteSpace(input))
            {
                value = -1;
            }
            else
            {
                try
                {
                    value = Convert.ToInt16(input);
                }
                catch (Exception)
                {
                    value = -1;
                }
            }
            return value == -1 || isDiff ? (value + 1) : value;
        }

        private void ImportXML(string xml)
        {
            if (!File.Exists(xml)) return;
            if (MessageBox.Show("Import XML file?\n\nThis might take a while if you have lots of songs...", AppName, MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question) != DialogResult.Yes) return;
            
            var new_songs = new List<SongData>();
            var sr = new StreamReader(xml, System.Text.Encoding.UTF8);
            var xmlLines = new List<string>();
            while (sr.Peek() >= 0)
            {
                xmlLines.Add(sr.ReadLine()
                        .Replace("&#39;", "'").Replace("&#38;", "&").Replace("&quot;", "\"").Replace("Ã©", "é")
                        .Replace("&amp;", "&").Replace("&Ouml;", "Ö").Replace("&iacute;", "í").Trim());
            }
            sr.Dispose();

            try
            {
                var line = xmlLines[0];
                if (line.Contains("xml"))
                {
                    line = xmlLines[1];
                }
                int type;
                switch (line)
                {
                    case "<songs>":
                        type = 0;
                        break;
                    case "<song>":
                        type = 1;
                        break;                    
                    default:
                        MessageBox.Show("Not a valid XML file to import", AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        return;
                }
                picWorking.Visible = true;
                picWorking.Refresh();
                var song = new SongData();
                for (var i = type == 1 ? 1 : 2; i < xmlLines.Count; i++)
                {
                    line = xmlLines[i];
                    switch (line)
                    {
                        case "<song>":
                            song = new SongData();
                            song.Initialize();
                            song.Master = true;
                            break;
                        case "</song>":
                            if (song.VocalsDiff == 0)
                            {
                                song.VocalParts = 0;
                            }
                            if (song.VocalParts == 0)
                            {
                                song.Gender = "N/A";
                            }
                            new_songs.Add(song);
                            picWorking.Refresh();
                            break;
                        default:
                            if (line.Contains("</song>") || line.Contains("</title>"))
                            {
                                song.Name = CleanXMLLine(line, line.Contains("</song>") ? "song" : "title");
                            }
                            else if (line.Contains("</artist>"))
                            {
                                song.Artist = CleanXMLLine(line, "artist");
                            }
                            else if (line.Contains("</id>"))
                            {
                                var id = CleanXMLLine(line, "id");
                                song.SongIdString = id;
                                try
                                {
                                    song.SongId = Convert.ToInt32(id);
                                }
                                catch (Exception)
                                {
                                    song.SongId = 0;
                                }
                            }
                            else if (line.Contains("</Vocal_Gender>"))
                            {
                                song.Gender = line.Contains("n\a") || line.Contains("none") ? "N/A" : (line.Contains("female") ? "Female" : "Male");
                            }
                            else if (line.Contains("</Vocal_Tracks>"))
                            {
                                song.VocalParts = CleanXMLValue(line, "Vocal_Tracks");
                            }
                            else if (line.Contains("</harmonies>"))
                            {
                                var harms = CleanXMLValue(line, "harmonies");
                                switch (harms)
                                {
                                    case 0:
                                        song.VocalParts = 1;
                                        break;
                                    default:
                                        song.VocalParts = harms;
                                        break;
                                }
                            }
                            else if (line.Contains("</album>"))
                            {
                                song.Album = CleanXMLLine(line, "album");
                            }
                            else if (line.Contains("</Song_Length>"))
                            {
                                var length = CleanXMLLine(line, "Song_Length");
                                if (string.IsNullOrWhiteSpace(length))
                                {
                                    song.Length = 0;
                                }
                                else
                                {
                                    try
                                    {
                                        song.Length = Convert.ToInt32(length);
                                    }
                                    catch (Exception)
                                    {
                                        song.Length = 0;
                                    }
                                }
                            }
                            else if (line.Contains("</Album_Track>"))
                            {
                                var track = CleanXMLValue(line, "Album_Track");
                                if (track == 0)
                                {
                                    track = 1;
                                }
                                song.TrackNumber = track;
                            }
                            else if (line.Contains("</genre>"))
                            {
                                var genre = CleanXMLLine(line, "genre");
                                song.Genre = string.IsNullOrWhiteSpace(genre) ? "Rock" : genre;
                            }
                            else if (line.Contains("</year>"))
                            {
                                song.YearReleased = CleanXMLValue(line, "year");
                                song.YearRecorded = song.YearReleased;
                            }
                            else if (line.Contains("</rating>"))
                            {
                                song.Rating = CleanXMLValue(line, "rating") + 1;
                            }
                            else if (line.Contains("</type>") || line.Contains("</disc>") || line.Contains("</pack>"))
                            {
                                var source = CleanXMLLine(line, line.Contains("</type>") ? "type" : (line.Contains("</pack>") ? "pack" : "disc"));
                                switch (source)
                                {
                                    case "RB1":
                                    case "Rock Band 1":
                                        song.Source = "rb1";
                                        break;
                                    case "RB2":
                                    case "Rock Band 2":
                                        song.Source = "rb2";
                                        break;
                                    case "RB3":
                                    case "Rock Band 3":
                                        song.Source = "rb3";
                                        break;
                                    case "RB4":
                                    case "Rock Band 4":
                                        song.Source = "rb4";
                                        break;
                                    case "RB:Network":
                                    case "RBN":
                                        song.Source = "rbn2";
                                        break;
                                    case "RB TP AC/DC Live":
                                    case "AC/DC Live":
                                        song.Source = "acdc";
                                        break;
                                    case "Blitz":
                                    case "Rock Band Blitz Soundtrack":
                                        song.Source = "blitz";
                                        break;
                                    case "Lego RB":
                                    case "LEGO Rock Band":
                                    case "LEGO Rock Band Import":
                                        song.Source = "lego";
                                        break;
                                    case "Green Day":
                                    case "Green Day: Rock Band":
                                    case "Green Day: Rock Band Import":
                                        song.Source = "gdrb";
                                        break;
                                    default:
                                        if (string.IsNullOrWhiteSpace(song.Source))
                                        {
                                            song.Source = "dlc";
                                        }
                                        break;
                                }
                            }
                            else if (line.Contains("</band>"))
                            {
                                var band = CleanXMLValue(line, "band");
                                song.BandDiff = band == 0 ? 1 : band;
                            }
                            else if (line.Contains("</guitar>"))
                            {
                                song.GuitarDiff = CleanXMLValue(line, "guitar", true);
                            }
                            else if (line.Contains("</pro_guitar>") || line.Contains("</pro guitar>"))
                            {
                                song.ProGuitarDiff = CleanXMLValue(line, line.Contains("_") ? "pro_guitar" : "pro guitar", true);
                            }
                            else if (line.Contains("</bass>"))
                            {
                                song.BassDiff = CleanXMLValue(line, "bass", true);
                            }
                            else if (line.Contains("</pro_bass>") || line.Contains("</pro bass>"))
                            {
                                song.ProBassDiff = CleanXMLValue(line, line.Contains("_") ? "pro_bass" : "pro bass", true);
                            }
                            else if (line.Contains("</vocals>"))
                            {
                                song.VocalsDiff = CleanXMLValue(line, "vocals", true);
                            }
                            else if (line.Contains("</drums>"))
                            {
                                song.DrumsDiff = CleanXMLValue(line, "drums", true);
                            }
                            else if (line.Contains("</pro_drums>") || line.Contains("</pro drums>"))
                            {
                                song.DrumsDiff = CleanXMLValue(line, line.Contains("_") ? "pro_drums" : "pro drums", true);
                            }
                            else if (line.Contains("</keyboard>"))
                            {
                                song.KeysDiff = CleanXMLValue(line, "keyboard", true);
                            }
                            else if (line.Contains("</pro_keyboard>") || line.Contains("</pro keys>"))
                            {
                                song.ProKeysDiff = CleanXMLValue(line, line.Contains("</pro keys>") ? "pro keys" : "pro_keyboard", true);
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error importing XML file:\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (new_songs[0].Artist == "artist" && new_songs[0].Name == "song")
            {
                new_songs.RemoveAt(0);
            }
            if (!new_songs.Any())
            {
                MessageBox.Show("No songs could be imported from that file", AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            FinalizeImport(new_songs);
            picWorking.Visible = false;
        }

        private void FinalizeImport(ICollection<SongData> newSongs, bool quiet = false)
        {
            var initialCount = Songs.Count;
            foreach (var song in from song in newSongs let exists = Songs.Any(s => s.Name == song.Name && s.Artist == song.Artist) where !exists select song)
            {
                Songs.Add(song);
            }
            if (!quiet)
            {
                string message;
                var diff = Songs.Count - initialCount;
                if (diff == newSongs.Count)
                {
                    message = "Added " + diff + " of " + newSongs.Count + " songs to your Setlist";
                }
                else if (diff == 0)
                {
                    message = "You already had all those songs in your Setlist, no songs added";
                }
                else
                {
                    message = "You already had some of those songs in your Setlist\nAdded " + diff + " of " + newSongs.Count + " songs to your Setlist";
                }
                MessageBox.Show(message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            SaveSetlist(ActiveSetlistPath);
            LoadSongs();
        }
        
        private void radioArtist_CheckedChanged(object sender, EventArgs e)
        {
            if (((RadioButton) sender).Checked && !string.IsNullOrWhiteSpace(txtSearch.Text.Trim()))
            {
                FiltersChanged(sender, e);
            }
        }
        
        private void howToUseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var message = Tools.ReadHelpFile("sm");
            var help = new HelpForm(AppName + " - Help", message, true);
            help.ShowDialog();
        }

        private void updater_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            var path = Application.StartupPath + "\\bin\\update.txt";
            Tools.DeleteFile(path);
            using (var client = new WebClient())
            {
                try
                {
                    client.DownloadFile("http://www.keepitfishy.com/rb4/manager/update.txt", path);
                }
                catch (Exception)
                { }
            }
        }

        private void updater_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            var path = Application.StartupPath + "\\bin\\update.txt";
            if (!File.Exists(path))
            {
                if (showMessage)
                {
                    MessageBox.Show("Unable to check for updates, try again later", AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                return;
            }
            var thisVersion = GetAppVersion();
            var newVersion = "v";
            string appName;
            string releaseDate;
            string link;
            var changeLog = new List<string>();
            var sr = new StreamReader(path);
            try
            {
                var line = sr.ReadLine();
                if (line.ToLowerInvariant().Contains("html"))
                {
                    if (showMessage)
                    {
                        MessageBox.Show("Unable to check for updates, try again later", AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                    sr.Dispose();
                    return;
                }
                appName = Tools.GetConfigString(line);
                newVersion += Tools.GetConfigString(sr.ReadLine());
                releaseDate = Tools.GetConfigString(sr.ReadLine());
                link = Tools.GetConfigString(sr.ReadLine());
                sr.ReadLine();//ignore Change Log header
                while (sr.Peek() >= 0)
                {
                    changeLog.Add(sr.ReadLine());
                }
            }
            catch (Exception ex)
            {
                if (showMessage)
                {
                    MessageBox.Show("Error parsing update file:\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                sr.Dispose();
                return;
            }
            sr.Dispose();
            Tools.DeleteFile(path);
            if (thisVersion.Equals(newVersion))
            {
                if (showMessage)
                {
                    MessageBox.Show("You have the latest version", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }
            var newInt = Convert.ToInt16(newVersion.Replace("v", "").Replace(".", "").Trim());
            var thisInt = Convert.ToInt16(thisVersion.Replace("v", "").Replace(".", "").Trim());
            if (newInt <= thisInt)
            {
                if (showMessage)
                {
                    MessageBox.Show("You have a newer version (" + thisVersion + ") than what's on the server (" + newVersion + ")\nNo update needed!", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }
            var updaterForm = new Updater();
            updaterForm.SetInfo(Text, thisVersion, appName, newVersion, releaseDate, link, changeLog);
            updaterForm.ShowDialog();
        }

        private static string GetAppVersion()
        {
            var vers = Assembly.GetExecutingAssembly().GetName().Version;
            return "v" + String.Format("{0}.{1}.{2}", vers.Major, vers.Minor, vers.Build);
        }

        private void checkForUpdates_Click(object sender, EventArgs e)
        {
            showMessage = true;
            updater.RunWorkerAsync();
        }

        private void aboutToolStrip_Click(object sender, EventArgs e)
        {
            var version = GetAppVersion();
            var message = Tools.ReadHelpFile("about");
            MessageBox.Show(AppName + " " + version + "\n" + message, "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void addGameSongs_Click(object sender, EventArgs e)
        {
            AddRB4Songs("songs_rb4");
        }

        private void addPreorderSongs_Click_1(object sender, EventArgs e)
        {
            AddRB4Songs("songs_rb4_pre");
        }

        private void addAmazonSongs_Click(object sender, EventArgs e)
        {
            AddRB4Songs("songs_rb4_amzn");
        }

        private void addPlayStationPlusSongs_Click(object sender, EventArgs e)
        {
            AddRB4Songs("songs_rb4_psp");
        }

        private void addXboxOneSongs_Click(object sender, EventArgs e)
        {
            AddRB4Songs("songs_rb4_xone");
        }

        private void addRB1ExportSongs_Click(object sender, EventArgs e)
        {
            AddRB4Songs("songs_rb1");
        }

        private void addRB2ExportSongs_Click(object sender, EventArgs e)
        {
            AddRB4Songs("songs_rb2");
        }

        private void addRB3ExportSongs_Click_1(object sender, EventArgs e)
        {
            AddRB4Songs("songs_rb3");
        }

        private void addLegoExportSongs_Click(object sender, EventArgs e)
        {
            AddRB4Songs("songs_lego");
        }

        private void addGreenDayExportSongs_Click(object sender, EventArgs e)
        {
            AddRB4Songs("songs_gdrb");
        }

        private void addACDCSongs_Click(object sender, EventArgs e)
        {
            AddRB4Songs("songs_acdc");
        }

        private void addBlitzSongs_Click(object sender, EventArgs e)
        {
            AddRB4Songs("songs_blitz");
        }

        private void forceRedownloading_Click(object sender, EventArgs e)
        {
            if (isLocked)
            {
                DoUnlockWarning();
                return;
            }
            if (downloader.IsBusy)
            {
                MessageBox.Show("Currently downloading from server, try again later", AppName, MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation);
                return;
            }
            var toDel = new List<string>
            {
                "rb1","rb2","rb3","rb4","rb4_amzn","rb4_pre","rb4_psp","rb4_xone","lego","gdrb","acdc","blitz"
            };
            foreach (var file in toDel)
            {
                Tools.DeleteFile(binFolder + "songs_" + file);
                UpdateRB4Features();
            }
            downloader.RunWorkerAsync();
        }

        private void loadSetlistToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (isLocked)
            {
                DoUnlockWarning();
                return;
            }
            if (unSaved)
            {
                if (MessageBox.Show("You have unsaved changes to your setlist\nAre you sure you want to lose those changes?\n\nClick NO to cancel and return to the program",
                        AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) != DialogResult.Yes)
                {
                    return;
                }
            }
            var file = GetFileToLoad();
            if (string.IsNullOrWhiteSpace(file)) return;
            if (!LoadSetlist(file)) return;
            if (LoadSongs())
            {
                SortSongs();
            }
        }

        private string GetFileToLoad(string type = "Setlist")
        {
            var ofd = new OpenFileDialog
            {
                InitialDirectory = setlist_folder,
                Title = "Select " + type + " file",
                Multiselect = false,
                Filter = type + " Files (*." + type.ToLowerInvariant() + ")|*" + type.ToLowerInvariant()
            };
            ofd.ShowDialog();
            return ofd.FileName;
        }

        private List<SongData> GrabSongsFromSetlist(string file, bool isLoading)
        {
            var SongsGrabbed = new List<SongData>();
            if (!File.Exists(file)) return SongsGrabbed;

            var line = "";
            var linenum = 5;
            var sr = new StreamReader(file, System.Text.Encoding.UTF8);
            try
            {
                int songcount;
                string format;
                if (isLoading)
                {
                    ActiveSetlist = Tools.GetConfigString(sr.ReadLine());
                    tabHolder.TabPages[0].Text = ActiveSetlist;
                    Text = "Setlist Manager - " + ActiveSetlist;
                    ActiveConsole = Tools.GetConfigString(sr.ReadLine());
                    songcount = Convert.ToInt16(Tools.GetConfigString(sr.ReadLine()));
                    sr.ReadLine();
                    format = Tools.GetConfigString(sr.ReadLine()).ToLowerInvariant();
                }
                else
                {
                    sr.ReadLine();
                    sr.ReadLine();
                    songcount = Convert.ToInt16(Tools.GetConfigString(sr.ReadLine()));
                    sr.ReadLine();
                    format = Tools.GetConfigString(sr.ReadLine()).ToLowerInvariant();
                }


                for (var i = 0; i < songcount; i++)
                {
                    try
                    {
                        SongsGrabbed.Add(new SongData());
                        var index = SongsGrabbed.Count - 1;

                        //all Setlist Manager cache formats
                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].Artist = Tools.GetConfigString(line);

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].Name = Tools.GetConfigString(line);

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].Album = Tools.GetConfigString(line);

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].TrackNumber = Convert.ToInt16(Tools.GetConfigString(line));

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].Master = line.Contains("True");

                        line = sr.ReadLine();
                        linenum++;
                        var year = Convert.ToInt16(Tools.GetConfigString(line));
                        SongsGrabbed[index].YearRecorded = year < 0 || year > 2020 ? 0 : year;

                        line = sr.ReadLine();
                        linenum++;
                        year = Convert.ToInt16(Tools.GetConfigString(line));
                        SongsGrabbed[index].YearReleased = year < 0 || year > 2020 ? 0 : year;

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].Genre = Tools.GetConfigString(line);

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].Rating = Convert.ToInt16(Tools.GetConfigString(line));

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].Gender = Tools.GetConfigString(line);

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].VocalParts = Convert.ToInt16(Tools.GetConfigString(line));

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].DrumsDiff = Convert.ToInt16(Tools.GetConfigString(line));

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].BassDiff = Convert.ToInt16(Tools.GetConfigString(line));

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].ProBassDiff = Convert.ToInt16(Tools.GetConfigString(line));

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].GuitarDiff = Convert.ToInt16(Tools.GetConfigString(line));

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].ProGuitarDiff = Convert.ToInt16(Tools.GetConfigString(line));

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].KeysDiff = Convert.ToInt16(Tools.GetConfigString(line));

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].ProKeysDiff = Convert.ToInt16(Tools.GetConfigString(line));

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].VocalsDiff = Convert.ToInt16(Tools.GetConfigString(line));

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].BandDiff = Convert.ToInt16(Tools.GetConfigString(line));

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].Length = Convert.ToInt32(Tools.GetConfigString(line));

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].ShortName = Tools.GetConfigString(line);

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].SongId = Convert.ToInt32(Tools.GetConfigString(line));

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].Source = Tools.GetConfigString(line);
                        if (string.IsNullOrWhiteSpace(SongsGrabbed[index].Source))
                        {
                            SongsGrabbed[index].Source = "dlc";
                        }

                        if (!format.Contains("2") && !format.Contains("3") && !format.Contains("4") && !format.Contains("5")) continue;
                        //Setlist Manager cache format 2-4, both RB3 and Blitz
                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].FilePath = Tools.GetConfigString(line);

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].PreviewStart = Convert.ToInt32(Tools.GetConfigString(line));

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].PreviewEnd = Convert.ToInt32(Tools.GetConfigString(line));

                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].GameVersion = Convert.ToInt16(Tools.GetConfigString(line));

                        if (!format.ToLowerInvariant().Contains("blitz"))
                        {
                            //Setlist Manager cache format 2-4, only RB3 
                            line = sr.ReadLine();
                            linenum++;
                            SongsGrabbed[index].ScrollSpeed = Convert.ToInt16(Tools.GetConfigString(line));

                            line = sr.ReadLine();
                            linenum++;
                            SongsGrabbed[index].TonicNote = Convert.ToInt16(Tools.GetConfigString(line));

                            line = sr.ReadLine();
                            linenum++;
                            SongsGrabbed[index].Tonality = Convert.ToInt16(Tools.GetConfigString(line));

                            line = sr.ReadLine();
                            linenum++;
                            SongsGrabbed[index].PercussionBank = Tools.GetConfigString(line);

                            line = sr.ReadLine();
                            linenum++;
                            SongsGrabbed[index].DrumBank = Tools.GetConfigString(line);
                        }

                        if (!format.Contains("3") && !format.Contains("4") && !format.Contains("5")) continue;
                        //Setlist Manager cache format 3-4, both RB3 and Blitz
                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].DoNotExport = line.Contains("True");

                        if (!format.Contains("4") && !format.Contains("5")) continue;
                        if (!format.ToLowerInvariant().Contains("blitz"))
                        {
                            //Setlist Manager cache format 4-5, only RB3
                            line = sr.ReadLine();
                            linenum++;
                            SongsGrabbed[index].ProBassTuning = Tools.GetConfigString(line);

                            //Setlist Manager cache format 4-5, only RB3
                            line = sr.ReadLine();
                            linenum++;
                            SongsGrabbed[index].ProGuitarTuning = Tools.GetConfigString(line);
                        }

                        if (!format.Contains("5")) continue;
                        //Setlist Manager cache format 5, RB3 and Blitz
                        line = sr.ReadLine();
                        linenum++;
                        SongsGrabbed[index].SongLink = Tools.GetConfigString(line);

                        //add further checks for newer cache versions here
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("There was a problem loading song #" + (i + 1) + " in Setlist file:\n'" + Path.GetFileName(file) + "'\n\nThe error says:\n'" +
                            ex.Message + "'\n\nLine:\t'" + line + "'\nLine #:\t" + linenum + "\n\nSkipping this song...", AppName, MessageBoxButtons.OK,
                            MessageBoxIcon.Exclamation);
                        SongsGrabbed.RemoveAt(SongsGrabbed.Count - 1);

                        //calculate how many lines until next song, then read/skip those lines
                        var lines = (5 + ((i + 1) * 34)) - linenum;
                        for (var x = 0; x < lines; x++)
                        {
                            sr.ReadLine();
                        }
                    }
                }
                sr.Dispose();
            }
            catch (Exception ex)
            {
                sr.Dispose();
                SongsGrabbed.Clear();
                MessageBox.Show("There was a problem loading Setlist '" + Path.GetFileName(file) + "'\n\nThe error says:\n'" +
                                ex.Message + "'\n\nTry re-importing if this problem continues", AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                picWorking.Visible = false;
            }
            return SongsGrabbed;
        }

        private bool LoadSetlist(string file)
        {
            if (!File.Exists(file))
            {
                MessageBox.Show("Setlist file '" + Path.GetFileName(ActiveSetlistPath) + "' does not exist, can't continue", AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return false;
            }

            wait = true;
            picWorking.Visible = true;
            picWorking.Refresh();
            DoFilterReset();
            ClearDetailedInfo();
            EnableDisableInfo(false);
            btnDeleteSong.Enabled = false;
            btnSave.Visible = false;
            IsUnSaved(false);
            
            Songs.Clear();
            lstSongs.Items.Clear();
            UpdateSongCounter();
            ActiveSetlistPath = file;

            Songs = GrabSongsFromSetlist(file, true);

            btnNew.Enabled = true;
            viewSetlistDetails.Enabled = true;
            picWorking.Visible = false;
            return true;
        }

        private void CreateBlankSetlist(string console)
        {
            if (isLocked)
            {
                DoUnlockWarning();
                return;
            }
            var name = "Unnamed " + console + " Setlist";
            var file = setlist_folder + name.Replace(" ", "") + DateTime.Now.Year + DateTime.Now.Month + DateTime.Now.Day + DateTime.Now.Hour + DateTime.Now.Minute + DateTime.Now.Second + ".setlist";
            var sw = new StreamWriter(file, false, Encoding.UTF8);
            sw.WriteLine("SetlistName=" + name);
            sw.WriteLine("Console=" + console);
            sw.WriteLine("SongCount=" + 0);
            sw.WriteLine("PackageCount=" + 0);
            sw.WriteLine("CacheFormat=" + ("RB_4"));
            sw.Dispose();
            ValidateFile(new[] { file });
            SaveOptions();
        }

        private void rB4XOne_Click(object sender, EventArgs e)
        {
            CreateBlankSetlist("Xbox One");
        }

        private void rB4PS4_Click(object sender, EventArgs e)
        {
            CreateBlankSetlist("PS4");
        }

        private void rB3X360_Click(object sender, EventArgs e)
        {
            CreateBlankSetlist("Xbox 360");
        }

        private void rB3PS3_Click(object sender, EventArgs e)
        {
            CreateBlankSetlist("PS3");
        }

        private void rB3Wii_Click(object sender, EventArgs e)
        {
            CreateBlankSetlist("Wii");
        }

        private void viewSetlistDetails_Click(object sender, EventArgs e)
        {
            if (Songs.Count == 0) return;
            var details = new SetlistDetails(this, btnSave.BackColor, btnSave.ForeColor)
            {
                Songs = Songs,
                ActiveConsole = ActiveConsole,
                ActiveSetlist = ActiveSetlist,
            };
            details.ShowDialog();
        }

        private void exportAsSetlist_Click(object sender, EventArgs e)
        {
            if (isLocked)
            {
                DoUnlockWarning();
                return;
            }
            if (Songs.Count == 0 || lstSongs.Items.Count == 0)
            {
                MessageBox.Show("No songs to export", AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            var sfd = new SaveFileDialog
            {
                FileName = "MyCustomSetlist",
                Title = "Select where to save .Setlist file",
                InitialDirectory = Tools.CurrentFolder,
                Filter = "Setlist File (*.setlist)|*setlist",
                OverwritePrompt = true,
                CheckPathExists = true,
            };
            if (sfd.ShowDialog() != DialogResult.OK || string.IsNullOrEmpty(Path.GetFileName(sfd.FileName))) return;
            var file = sfd.FileName.EndsWith(".setlist") ? sfd.FileName : sfd.FileName + ".setlist";
            Tools.CurrentFolder = Path.GetDirectoryName(file);
            Tools.DeleteFile(file);
            SaveSetlist(file, GetSongsToExport());
            Application.DoEvents();
            var success = File.Exists(file);
            MessageBox.Show(success ? "Exported custom .Setlist file successfully" : "Failed to export custom .Setlist file",
                AppName, MessageBoxButtons.OK, success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
        }

        private List<SongData> GetSongsToExport()
        {
            var songs = new List<SongData>();
            for (var i = 0; i < lstSongs.Items.Count; i++)
            {
                var id = Convert.ToInt16(lstSongs.Items[i].Tag);
                if (Songs[id].DoNotExport) continue; //skip songs marked DoNotExport
                songs.Add(Songs[id]);
                var index = songs.Count - 1;
                var artist = CleanName(songs[index].Artist);
                var name = CleanName(songs[index].Name);
                if (moveFeaturedArtistsToSongName.Checked)
                {
                    var featured = MoveFeatArtist(artist, name);
                    if (featured.Count > 0)
                    {
                        artist = featured[0];
                        name = featured[1];
                    }
                }
                songs[index].Artist = artist;
                songs[index].Name = name;
            }
            return songs;
        }

        private void exportSetlist_Click(object sender, EventArgs e)
        {
            if (isLocked)
            {
                DoUnlockWarning();
                return;
            }
            var exporter = new SetlistExporter
            {
                Text = "Setlist Exporter - " + ActiveSetlist,
                setlistname = ActiveSetlist,
                setlistpath = ActiveSetlistPath,
                console = ActiveConsole,
                isRB4 = modeRB4.Checked
            };
            exporter.SetGame(modeRB4.Checked ? Resources.icon_rb4 : Resources.icon_rb3);
            exporter.Songs = GetSongsToExport();
            exporter.SortByArtist = lstSongs.Columns[ActiveSortColumn].Text == "Artist";
            exporter.ShowDialog();
        }

        private void importSetlistFile_Click(object sender, EventArgs e)
        {
            if (isLocked)
            {
                DoUnlockWarning();
                return;
            }
            if (string.IsNullOrWhiteSpace(ActiveSetlist) || !File.Exists(ActiveSetlistPath))
            {
                MessageBox.Show("Create a Setlist first", AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            var file = GetFileToLoad();
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return;
            var songs = GrabSongsFromSetlist(file, false);
            FinalizeImport(songs);
        }

        private void importDLCQuickplay_Click(object sender, EventArgs e)
        {
            ImportDLC(1);
        }

        private void importMyRockBandSongs_Click(object sender, EventArgs e)
        {
            ImportDLC(0);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (isLocked)
            {
                MessageBox.Show("You must unlock the program first", "Locked", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                Close();
            }
        }

        private void viewSetlistDetails_EnabledChanged(object sender, EventArgs e)
        {
            var enabled = viewSetlistDetails.Enabled;
            exportSetlist.Enabled = enabled;
            exportAsSetlist.Enabled = enabled;
            importSetlistFile.Enabled = !string.IsNullOrEmpty(ActiveSetlistPath);
            importDtaFile.Enabled = !string.IsNullOrEmpty(ActiveSetlistPath);
            importMyRockBandSongs.Enabled = !string.IsNullOrEmpty(ActiveSetlistPath);
            importDLCQuickplay.Enabled = !string.IsNullOrEmpty(ActiveSetlistPath);
            correctMetadataForRB4.Enabled = (enabled || Songs.Count > 0) && modeRB4.Checked;
            findExactDuplicates.Enabled = enabled || Songs.Count > 0;
            findPossibleDuplicates.Enabled = enabled || Songs.Count > 0;
            findUnsupportedCharacters.Enabled = enabled || Songs.Count > 0;
            findSongsMarkedDoNotExport.Enabled = enabled || Songs.Count > 0;
            findAndLabelBlitzSongs.Enabled = enabled || Songs.Count > 0;
        }

        private void importDtaFile_Click(object sender, EventArgs e)
        {
            if (isLocked)
            {
                DoUnlockWarning();
                return;
            }
            if (string.IsNullOrWhiteSpace(ActiveSetlist) || !File.Exists(ActiveSetlistPath))
            {
                MessageBox.Show("Create a Setlist first", AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            var file = GetFileToLoad("DTA");
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return;
            if (Parser.ReadDTA(File.ReadAllBytes(file)))
            {
                FinalizeImport(Parser.Songs);
            }
            else
            {
                MessageBox.Show("Failed to import .dta file", AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void openLinkInBrowser_Click(object sender, EventArgs e)
        {
            if (isLocked)
            {
                DoUnlockWarning();
                return;
            }
            string link;
            try
            {
                var index = Convert.ToInt16(lstSongs.SelectedItems[0].Tag);
                link = Songs[index].SongLink;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error determining which song is selected:\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (string.IsNullOrWhiteSpace(link))
            {
                MessageBox.Show("Invalid or empty link", AppName, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                openLinkInBrowser.Enabled = false;
            }
            else
            {
                Process.Start(link);
            }
        }

        private void editLinkToSong_Click(object sender, EventArgs e)
        {
            if (isLocked)
            {
                DoUnlockWarning();
                return;
            }
            int index;
            try
            {
                index = Convert.ToInt16(lstSongs.SelectedItems[0].Tag);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error determining which song is selected:\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (index < 0 || index > (lstSongs.Items.Count - 1)) return;
            var link = Interaction.InputBox("Enter link to song:", AppName, Songs[index].SongLink);
            if (string.IsNullOrWhiteSpace(link))
            {
                openLinkInBrowser.Enabled = false;
            }
            Songs[index].SongLink = link;
            lstSongs.SelectedItems[0].SubItems[lstSongs.Columns.IndexOf(colLink)].Text = link;
            SaveSetlist(ActiveSetlistPath);
        }

        private void resetWindowSize_Click(object sender, EventArgs e)
        {
            Size = new Size(831, 760);
        }

        private void SetlistManager_Resize(object sender, EventArgs e)
        {
            btnSave.Left = btnNew.Left + btnNew.Width + (btnDeleteSong.Left - btnNew.Left - btnNew.Width - btnSave.Width) / 2;
        }

        private void editColumnsToolStrip_Click(object sender, EventArgs e)
        {
            lstSongs.Enabled = false;
            EditColumns();
        }

        private void btnColShow_Click(object sender, EventArgs e)
        {
            ShowHideColumns(true);
        }

        private void btnColHide_Click(object sender, EventArgs e)
        {
            ShowHideColumns(false);
        }

        private void ShowHideColumns(bool show)
        {
            foreach (var box in ColumnBoxes)
            {
                box.Checked = show;
            }
        }

        private void EditColumns()
        {
            foreach (var box in ColumnBoxes)
            {
                var index = Convert.ToInt16(box.Tag);
                box.Checked = Columns[index].Visible;
            }
            grpColumns.BringToFront();
            grpColumns.Visible = true;
        }

        private void btnColSave_Click(object sender, EventArgs e)
        {
            foreach (var box in ColumnBoxes)
            {
                ModifyColumn(box);
            }
            grpColumns.Visible = false;
            lstSongs.Enabled = true;
        }

        private void ModifyColumn(CheckBox box)
        {
            var index = Convert.ToInt16(box.Tag);
            Columns[index].Visible = box.Checked;
            if (lstSongs.Columns[index].Width > 0)
            {
                Columns[index].currWidth = lstSongs.Columns[index].Width;
            }
            lstSongs.Columns[index].Width = Columns[index].Visible ? Columns[index].currWidth : 0;
        }

        private void chkColArtist_CheckedChanged(object sender, EventArgs e)
        {
            ModifyColumn((CheckBox)sender);
        }

        private void useTierNames_Click(object sender, EventArgs e)
        {
            useTierNames.Checked = false;
            useTierNumbers.Checked = false;
            useTierDots.Checked = false;
            ((ToolStripMenuItem) sender).Checked = true;
            LoadSongs();
        }

        private void discussOnReddit_Click(object sender, EventArgs e)
        {
            Process.Start("https://www.reddit.com/r/Rockband/comments/43168i/setlist_manager_manage_view_and_print_your_rock/");
        }

        private void discussOnHMXForums_Click(object sender, EventArgs e)
        {
            Process.Start("http://forums.harmonixmusic.com/discussion/262703/setlist-manager-manage-view-and-print-your-rock-band-song-list");
        }

        private void btnResetColOrder_Click(object sender, EventArgs e)
        {
            for (var i = 0; i < lstSongs.Columns.Count; i++)
            {
                lstSongs.Columns[i].DisplayIndex = i;
            }
        }

        private void btnResetColSize_Click(object sender, EventArgs e)
        {
            for (var i = 0; i < lstSongs.Columns.Count; i++)
            {
                lstSongs.Columns[i].Width = Columns[i].origWidth;
            }
        }
    }
    
    // Implements the manual sorting of items by columns.
    class ListViewItemComparer : IComparer
    {
        private readonly int col;
        private readonly SortOrder order;
        private readonly ListView sender;

        public ListViewItemComparer()
        {
            col = 0;
            order = SortOrder.Ascending;
        }

        public ListViewItemComparer(ListView sender, int column, SortOrder order)
        {
            col = column;
            this.sender = sender;
            this.order = order;
        }

        private static long GetMultiplier(string text)
        {
            const long MB = 1048576;
            const long GB = 1073741824;
            const long TB = 1099511627776;
            long multiplier;
            if (text.ToLowerInvariant().Contains("tb"))
            {
                multiplier = TB;
            }
            else if (text.ToLowerInvariant().Contains("gb"))
            {
                multiplier = GB;
            }
            else if (text.ToLowerInvariant().Contains("mb"))
            {
                multiplier = MB;
            }
            else
            {
                multiplier = 1024;
            }
            return multiplier;
        }

        private static int EnumDifficulty(string difficulty)
        {
            switch (difficulty)
            {
                case "No Part":
                case "xxxxx":
                case "0":
                    return 0;
                case "Warmup":
                case "ooooo":
                case "1":
                    return 1;
                case "Apprentice":
                case "Ooooo":
                case "2":
                    return 2;
                case "Solid":
                case "OOooo":
                case "3":
                    return 3;
                case "Moderate":
                case "OOOoo":
                case "4":
                    return 4;
                case "Challenging":
                case "OOOOo":
                case "5":
                    return 5;
                case "Nightmare":
                case "OOOOO":
                case "6":
                    return 6;
                case "Impossible":
                case "●●●●●":
                case "7":
                    return 7;
                default:
                    return 0;
            }
        }

        public int Compare(object x, object y)
        {
            int returnVal;
            string text1;
            string text2;
            //in case of the active column being one that was removed, will throw error
            try
            {
                text1 = ((ListViewItem)x).SubItems[col].Text.Replace("\"","");
                text2 = ((ListViewItem)y).SubItems[col].Text.Replace("\"", "");
            }
            catch (Exception)
            {
                return 0;
            }
            //ignore leading "The " when sorting (i.e. sort The Beatles under B)
            if (text1.ToLowerInvariant().StartsWith("the ", StringComparison.Ordinal))
            {
                text1 = text1.Substring(4, text1.Length - 4);
            }
            if (text2.ToLowerInvariant().StartsWith("the ", StringComparison.Ordinal))
            {
                text2 = text2.Substring(4, text2.Length - 4);
            }
            switch ((string) sender.Columns[col].Tag)
            {
                case "Numeric":
                    try
                    {
                        var num1 = Convert.ToInt16(text1);
                        var num2 = Convert.ToInt16(text2);
                        returnVal = num1.CompareTo(num2);
                    }
                    catch (Exception) //if this fails, try as string
                    {
                        returnVal = String.CompareOrdinal(text1.ToLowerInvariant(), text2.ToLowerInvariant());
                    }
                    break;
                case "FileSize":
                    try
                    {
                        var size1 = Convert.ToDouble(text1.Substring(0, text1.IndexOf(" ", StringComparison.Ordinal)));
                        var size2 = Convert.ToDouble(text2.Substring(0, text2.IndexOf(" ", StringComparison.Ordinal)));
                        returnVal = (size1 * GetMultiplier(text1)).CompareTo(size2 * GetMultiplier(text2));
                    }
                    catch (Exception) //if this fails, try as string
                    {
                        returnVal = String.CompareOrdinal(text1.ToLowerInvariant(), text2.ToLowerInvariant());
                    }
                    break;
                case "Time":
                    try
                    {
                        var num1 = Convert.ToInt16(text1.Replace(":",""));
                        var num2 = Convert.ToInt16(text2.Replace(":", ""));
                        returnVal = num1.CompareTo(num2);
                    }
                    catch (Exception) //if this fails, try as string
                    {
                        returnVal = String.CompareOrdinal(text1.ToLowerInvariant(), text2.ToLowerInvariant());
                    }
                    break;
                case "DateTime":
                    try
                    {
                        var date1 = DateTime.Parse(text1);
                        var date2 = DateTime.Parse(text2);
                        returnVal = date1.CompareTo(date2);
                    }
                    catch (Exception) //if this fails, try as string
                    {
                        returnVal = String.CompareOrdinal(text1.ToLowerInvariant(), text2.ToLowerInvariant());
                    }
                    break;
                case "Difficulty":
                    try
                    {
                        var num1 = EnumDifficulty(text1);
                        var num2 = EnumDifficulty(text2);
                        returnVal = num1.CompareTo(num2);
                    }
                    catch (Exception) //if this fails, try as string
                    {
                        returnVal = String.CompareOrdinal(text1.ToLowerInvariant(), text2.ToLowerInvariant());
                    }
                    break;
                default:
                    returnVal = String.CompareOrdinal(text1.ToLowerInvariant(), text2.ToLowerInvariant());
                    break;
            }
            // Determine whether the sort order is descending.
            if (order == SortOrder.Descending)
            {
                returnVal *= -1; // Invert the value returned by String.Compare.
            }
            return returnVal;
        }
    }

    public class ColumnInfo
    {
        public int origWidth { get; set; }
        public int currWidth { get; set; }
        public bool Visible { get; set; }
    }
}
