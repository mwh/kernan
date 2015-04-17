using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

namespace Grace.Unicode
{
    class UnicodeLookup
    {
        /// <summary>Get the UnicodeData.txt elements for a
        /// particular codepoint</summary>
        /// <param name="codepointOrig">Codepoint to look up</param>
        /// <remarks>This method uses a lookup table based on the
        /// newest version of the Unicode standard. It automatically
        /// fills in the entries for codepoints within First, Last
        /// ranges in the table.</remarks>
        public static string[] GetCodepointParts(int codepointOrig)
        {
            string dir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string fp = Path.Combine(dir, "UnicodeLookupTable.dat");
            int codepoint = codepointOrig;
            string[] parts;
            using (FileStream stream = File.OpenRead(fp))
            {
                stream.Seek(4 * codepoint, SeekOrigin.Begin);
                byte[] bytes = new byte[4];
                stream.Read(bytes, 0, 4);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(bytes);
                int offset = BitConverter.ToInt32(bytes, 0);
                if (offset != 0)
                {
                    byte[] line = new byte[256];
                    stream.Seek(offset, SeekOrigin.Begin);
                    stream.Read(line, 0, 256);
                    string partString = System.Text.Encoding.ASCII.GetString(line, 0, Array.IndexOf(line, (byte)0));
                    parts = partString.Split(';');
                }
                else
                {
                    string cps = codepoint.ToString("6X");
                    string name = "UNICODE CHARACTER U+" + cps;
                    string infoLine = null;
                    if (codepoint >= 0x3400 && codepoint <= 0x4dbf)
                    {
                        name = "CJK IDEOGRAPH EXTENSION A-" + cps;
                        infoLine = "<CJK Ideograph Extension A, First>;Lo;0;L;;;;;N;;;;;";
                    }
                    else if (codepoint >= 0x4e00 && codepoint <= 0x9fcc)
                    {
                        name = "CJK IDEOGRAPH-" + cps;
                        infoLine = "<CJK Ideograph, First>;Lo;0;L;;;;;N;;;;;";
                    }
                    else if (codepoint >= 0xac00 && codepoint <= 0xd7a3)
                    {
                        name = "HANGUL SYLLABLE-" + cps;
                        infoLine = "<Hangul Syllable, First>;Lo;0;L;;;;;N;;;;;";
                    }
                    else if (codepoint >= 0xd800 && codepoint <= 0xdb7f)
                    {
                        name = "NON PRIVATE USE HIGH SURROGATE-" + cps;
                        infoLine = "<Non Private Use High Surrogate, First>;Cs;0;L;;;;;N;;;;;";
                    }
                    else if (codepoint >= 0xdb80 && codepoint <= 0xdbff)
                    {
                        name = "PRIVATE USE HIGH SURROGATE-" + cps;
                        infoLine = "<Private Use High Surrogate, First>;Cs;0;L;;;;;N;;;;;";
                    }
                    else if (codepoint >= 0xdc00 && codepoint <= 0xdfff)
                    {
                        name = "LOW SURROGATE-" + cps;
                        infoLine = "<Low Surrogate, First>;Cs;0;L;;;;;N;;;;;";
                    }
                    else if (codepoint >= 0xe000 && codepoint <= 0xf8ff)
                    {
                        name = "PRIVATE USE" + cps;
                        infoLine = "<Private Use, First>;Co;0;L;;;;;N;;;;;";
                    }
                    else if (codepoint >= 0x20000 && codepoint <= 0x2a6df)
                    {
                        name = "CJK IDEOGRAPH EXTENSION B-" + cps;
                        infoLine = "<CJK Ideograph Extension B, First>;Lo;0;L;;;;;N;;;;;";
                    }
                    else if (codepoint >= 0x2A700 && codepoint <= 0x2B73F)
                    {
                        name = "CJK IDEOGRAPH EXTENSION C-" + cps;
                        infoLine = "<CJK Ideograph Extension C, First>;Lo;0;L;;;;;N;;;;;";
                    }
                    else if (codepoint >= 0x2b740 && codepoint <= 0x2b81f)
                    {
                        name = "CJK IDEOGRAPH EXTENSION D-" + cps;
                        infoLine = "<CJK Ideograph Extension D, First>;Lo;0;L;;;;;N;;;;;";
                    }
                    else if (codepoint >= 0xf0000 && codepoint <= 0xffffd)
                    {
                        name = "PLANE 15 PRIVATE USE-" + cps;
                        infoLine = "<Plane 15 Private Use, First>;Co;0;L;;;;;N;;;;;";
                    }
                    else if (codepoint >= 0x100000 && codepoint <= 0x10fffd)
                    {
                        name = "PLANE 16 PRIVATE USE-" + cps;
                        infoLine = "<Plane 16 Private Use, First>;Co;0;L;;;;;N;;;;;";
                    }
                    if (infoLine != null)
                    {
                        parts = infoLine.Split(';');
                        parts[0] = name;
                    }
                    else
                    {
                        parts = new string[14] {
                            name,
                            "", "", "", "", "", "", "", "", "", "", "", "", ""
                        };
                    }
                }
            }
            return parts;
        }

        /// <summary>Get the name of a codepoint</summary>
        /// <param name="codepoint">Codepoint to look up</param>
        public static string GetCodepointName(int codepoint)
        {
            string[] parts = GetCodepointParts(codepoint);
            return parts[0];
        }

        /// <summary>Get the category of a codepoint</summary>
        /// <param name="s">String to find codepoint in</param>
        /// <param name="index">char index of the start of the
        /// codepoint</param>
        public static UnicodeCategory GetUnicodeCategory(string s, int index)
        {
            UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(s, index);
            if (cat == UnicodeCategory.OtherNotAssigned)
            {
                string[] parts = GetCodepointParts(Char.ConvertToUtf32(s, index));
                return categoryFromString(parts[1]);
            }
            return cat;
        }

        private static UnicodeCategory categoryFromString(string c)
        {
            switch (c)
            {
                case "Lu": return UnicodeCategory.UppercaseLetter;
                case "Ll": return UnicodeCategory.LowercaseLetter;
                case "Lt": return UnicodeCategory.TitlecaseLetter;
                case "Lm": return UnicodeCategory.ModifierLetter;
                case "Lo": return UnicodeCategory.OtherLetter;
                case "Mn": return UnicodeCategory.NonSpacingMark;
                case "Mc": return UnicodeCategory.SpacingCombiningMark;
                case "Me": return UnicodeCategory.EnclosingMark;
                case "Nd": return UnicodeCategory.DecimalDigitNumber;
                case "Nl": return UnicodeCategory.LetterNumber;
                case "No": return UnicodeCategory.OtherNumber;
                case "Pc": return UnicodeCategory.ConnectorPunctuation;
                case "Pd": return UnicodeCategory.DashPunctuation;
                case "Ps": return UnicodeCategory.OpenPunctuation;
                case "Pe": return UnicodeCategory.ClosePunctuation;
                case "Pi": return UnicodeCategory.InitialQuotePunctuation;
                case "Pf": return UnicodeCategory.FinalQuotePunctuation;
                case "Po": return UnicodeCategory.OtherPunctuation;
                case "Sm": return UnicodeCategory.MathSymbol;
                case "Sc": return UnicodeCategory.CurrencySymbol;
                case "Sk": return UnicodeCategory.ModifierSymbol;
                case "So": return UnicodeCategory.OtherSymbol;
                case "Zs": return UnicodeCategory.SpaceSeparator;
                case "Zl": return UnicodeCategory.LineSeparator;
                case "Zp": return UnicodeCategory.ParagraphSeparator;
                case "Cc": return UnicodeCategory.Control;
                case "Cf": return UnicodeCategory.Format;
                case "Cs": return UnicodeCategory.Surrogate;
                case "Co": return UnicodeCategory.PrivateUse;
                case "Cn": return UnicodeCategory.OtherNotAssigned;
            }
            return UnicodeCategory.OtherNotAssigned;
        }
    }
}
