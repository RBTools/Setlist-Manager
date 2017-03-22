using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SetlistManager
{
    public class MyTools
    {
        public string CurrentFolder = ""; //used throughout the program to maintain the current working folder
        
        #region Misc Stuff

        public int GetDiffTag(Control instrument)
        {
            int diff;
            try
            {
                diff = Convert.ToInt16(instrument.Tag);
            }
            catch (Exception)
            {
                return 0;
            }
            return diff;
        }
        
        /// <summary>
        /// Returns line with featured artist normalized as 'ft.'
        /// </summary>
        /// <param name="line">Line to normalize</param>
        /// <returns></returns>
        public string FixFeaturedArtist(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return "";
            var adjusted = line;
            adjusted = adjusted.Replace("Featuring", "ft.");
            adjusted = adjusted.Replace("featuring", "ft.");
            adjusted = adjusted.Replace("feat.", "ft.");
            adjusted = adjusted.Replace("Feat.", "ft.");
            adjusted = adjusted.Replace(" ft ", " ft. ");
            adjusted = adjusted.Replace(" FT ", " ft. ");
            adjusted = adjusted.Replace("Ft. ", "ft. ");
            adjusted = adjusted.Replace("FEAT. ", "ft. ");
            adjusted = adjusted.Replace(" FEAT ", " ft. ");
            if (adjusted.StartsWith("ft ", StringComparison.Ordinal))
            {
                adjusted = "ft. " + adjusted.Substring(3, adjusted.Length - 3);
            }
            return FixBadChars(adjusted);
        }

        /// <summary>
        /// Loads and formats help file for display on the HelpForm
        /// </summary>
        /// <param name="file">Name of the file, path assumed to be \bin\help/</param>
        /// <returns></returns>
        public string ReadHelpFile(string file)
        {
            var message = string.Empty;
            var helpfile = Application.StartupPath + "\\bin\\help\\" + file;
            if (File.Exists(helpfile))
            {
                var sr = new StreamReader(helpfile);
                while (sr.Peek() > 0)
                {
                    var line = sr.ReadLine();
                    message = message == string.Empty ? line : message + "\r\n" + line;
                }
                sr.Dispose();
            }
            else
            {
                message = "Could not find help file, please redownload this program and DO NOT delete any files";
            }
            return message;
        }

        /// <summary>
        /// Use to quickly grab value on right side of = in options/fix files
        /// </summary>
        /// <param name="raw_line">Raw line from the text file</param>
        /// <returns></returns>
        public string GetConfigString(string raw_line)
        {
            if (string.IsNullOrWhiteSpace(raw_line)) return "";
            var line = raw_line;
            try
            {
                var index = line.IndexOf("=", StringComparison.Ordinal) + 1;
                line = line.Substring(index, line.Length - index);
            }
            catch (Exception)
            {
                line = "";
            }
            return line.Trim();
        }
        
        /// <summary>
        /// Returns string with correctly formatted characters
        /// </summary>
        /// <param name="raw_line">Raw line from songs.dta file</param>
        /// <returns></returns>
        public string FixBadChars(string raw_line)
        {
            var line = raw_line.Replace("Ã¡", "á");
            line = line.Replace("Ã©", "é");
            line = line.Replace("Ã¨", "è");
            line = line.Replace("ÃŠ", "Ê");
            line = line.Replace("Ã¬", "ì");
            line = line.Replace("Ã­­­", "í");
            line = line.Replace("Ã¯", "ï");
            line = line.Replace("Ã–", "Ö");
            line = line.Replace("Ã¶", "ö");
            line = line.Replace("Ã³", "ó");
            line = line.Replace("Ã²", "ò");
            line = line.Replace("Ãœ", "Ü");
            line = line.Replace("Ã¼", "ü");
            line = line.Replace("Ã¹", "ù");
            line = line.Replace("Ãº", "ú");
            line = line.Replace("Ã¿", "ÿ");
            line = line.Replace("Ã±", "ñ");
            line = line.Replace("ï¿½", "");
            line = line.Replace("�", "");
            line = line.Replace("E½", "");
            return line;
        }
        
        /// <summary>
        /// Returns byte array in hex value
        /// </summary>
        /// <param name="xIn">String value to be converted to hex</param>
        /// <returns></returns>
        public byte[] ToHex(string xIn)
        {
            for (var i = 0; i < (xIn.Length%2); i++)
            {
                xIn = "0" + xIn;
            }
            var xReturn = new List<byte>();
            for (var i = 0; i < (xIn.Length/2); i++)
            {
                xReturn.Add(Convert.ToByte(xIn.Substring(i * 2, 2), 16));
            }
            return xReturn.ToArray();
        }
        
        /// <summary>
        /// Returns true if the package description suggests a pack
        /// </summary>
        /// <param name="desc">Package description</param>
        /// <param name="disp">Package display</param>
        /// <returns></returns>
        public bool DescribesPack(string desc, string disp)
        {
            var description = desc.ToLowerInvariant();
            var display = disp.ToLowerInvariant();
            return (display.Contains("pro upgrade") || description.Contains("pro upgrade") ||
                   description.Contains("(pro)") || description.Contains("(upgrade)") ||
                   display.Contains("(pro)") || display.Contains("(upgrade)") ||
                   display.Contains("album") || description.Contains("album") ||
                   display.Contains("export") || description.Contains("export"));
        }

        /// <summary>
        /// Returns cleaned string for file names, etc
        /// </summary>
        /// <param name="raw_string">Raw string from the songs.dta file</param>
        /// <param name="removeDash">Whether to remove dashes from the string</param>
        /// <param name="DashForSlash">Whether to replace slashes with dashes</param>
        /// <returns></returns>
        public string CleanString(string raw_string, bool removeDash, bool DashForSlash = false)
        {
            var mystring = raw_string;

            //remove forbidden characters if present
            mystring = mystring.Replace("\"", "");
            mystring = mystring.Replace(">", " ");
            mystring = mystring.Replace("<", " ");
            mystring = mystring.Replace(":", " ");
            mystring = mystring.Replace("|", " ");
            mystring = mystring.Replace("?", " ");
            mystring = mystring.Replace("*", " ");
            mystring = mystring.Replace("&#8217;", "'"); //Don't Speak
            mystring = mystring.Replace("   ", " ");
            mystring = mystring.Replace("  ", " ");
            mystring = FixBadChars(mystring).Trim();

            if (removeDash)
            {
                if (mystring.Substring(0, 1) == "-") //if starts with -
                {
                    mystring = mystring.Substring(1, mystring.Length - 1);
                }
                if (mystring.Substring(mystring.Length - 1, 1) == "-") //if ends with -
                {
                    mystring = mystring.Substring(0, mystring.Length - 1);
                }

                mystring = mystring.Trim();
            }

            if (mystring.EndsWith(".", StringComparison.Ordinal)) //can't have files like Y.M.C.A.
            {
                mystring = mystring.Substring(0, mystring.Length - 1);
            }

            mystring = mystring.Replace("\\", DashForSlash && mystring != "AC/DC" ? "-" : (mystring != "AC/DC" ? " " : ""));
            mystring = mystring.Replace("/", DashForSlash && mystring != "AC/DC" ? "-" : (mystring != "AC/DC" ? " " : ""));

            return mystring;
        }

        /// <summary>
        /// Simple function to safely delete files
        /// </summary>
        /// <param name="file">Full path of file to be deleted</param>
        public void DeleteFile(string file)
        {
            if (string.IsNullOrWhiteSpace(file)) return;
            if (!File.Exists(file)) return;
            try
            {
                File.Delete(file);
            }
            catch (Exception)
            { }
        }

        public Color DarkenColor(Color color)
        {
            var correctionFactor = (float)-0.25;

            var red = (float)color.R;
            var green = (float)color.G;
            var blue = (float)color.B;

            if (correctionFactor < 0)
            {
                correctionFactor = 1 + correctionFactor;
                red *= correctionFactor;
                green *= correctionFactor;
                blue *= correctionFactor;
            }
            else
            {
                red = (255 - red) * correctionFactor + red;
                green = (255 - green) * correctionFactor + green;
                blue = (255 - blue) * correctionFactor + blue;
            }
            return Color.FromArgb(color.A, (int)red, (int)green, (int)blue);
        }

        public Color LightenColor(Color color)
        {
            var correctionFactor = (float)0.20;

            var red = (float)color.R;
            var green = (float)color.G;
            var blue = (float)color.B;

            if (correctionFactor < 0)
            {
                correctionFactor = 1 + correctionFactor;
                red *= correctionFactor;
                green *= correctionFactor;
                blue *= correctionFactor;
            }
            else
            {
                red = (255 - red) * correctionFactor + red;
                green = (255 - green) * correctionFactor + green;
                blue = (255 - blue) * correctionFactor + blue;
            }
            return Color.FromArgb(color.A, (int)red, (int)green, (int)blue);
        }

        /// <summary>
        /// Loads image and unlocks file for uses elsewhere. USE THIS!
        /// </summary>
        /// <param name="file">Full path to the image file</param>
        /// <returns></returns>
        public Image LoadImage(string file)
        {
            if (!File.Exists(file)) return null;
            Image img;
            using (var bmpTemp = new Bitmap(file))
            {
                img = new Bitmap(bmpTemp);
            }
            return img;
        }

        #endregion
    }
}
