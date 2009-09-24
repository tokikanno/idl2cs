using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;

namespace idl2cs
{
    class TextMarker
    {
        // /* */ type
        public static TextMarker ScanComment1(string in_txt, int start)
        {
            int length = in_txt.Length;
            int p = start + 1;

            while (p < length)
            {
                if (in_txt[p] == '*')
                    if (p + 1 < length)
                        if (in_txt[p + 1] == '/')
                        {
                            p++;
                            return new TextMarker(start - 1, p - start + 2, E_TextMarkerType.Comment);
                        }
                p++;
            }
            return null;
        }

        #region Load regular expression from resrouce
        public static System.Resources.ResourceManager rm = new System.Resources.ResourceManager("idl2cs.res", Assembly.GetExecutingAssembly());
        public static Regex find_enum = new Regex(rm.GetString("find_enum"), RegexOptions.Compiled | RegexOptions.Multiline);
        public static Regex find_typedef = new Regex(rm.GetString("find_typedef"), RegexOptions.Compiled | RegexOptions.Singleline);
        public static Regex find_interface = new Regex(rm.GetString("find_interface"), RegexOptions.Compiled | RegexOptions.Multiline);
        public static Regex find_function = new Regex(rm.GetString("find_function"), RegexOptions.Compiled | RegexOptions.Multiline);
        //Regex find_comment = new Regex(rm.GetString("find_comment"), RegexOptions.Compiled | RegexOptions.Multiline);
        public static Regex find_uuid = new Regex(rm.GetString("find_uuid"), RegexOptions.Compiled | RegexOptions.Multiline);
        public static Regex find_helpstring = new Regex(rm.GetString("find_helpstring"), RegexOptions.Compiled | RegexOptions.Multiline);
        public static Regex find_func_attr = new Regex(rm.GetString("find_func_attr"), RegexOptions.Compiled | RegexOptions.Singleline);
        public static Regex find_coclass = new Regex(rm.GetString("find_coclass"), RegexOptions.Compiled | RegexOptions.Multiline);
        public static Regex find_include = new Regex(rm.GetString("find_include"), RegexOptions.Compiled | RegexOptions.Multiline);
        public static Regex find_fwd_interface = new Regex(rm.GetString("find_fwd_interface"), RegexOptions.Compiled | RegexOptions.Multiline);
        public static Regex find_whole_line_comment = new Regex(rm.GetString("find_whole_line_comment"), RegexOptions.Compiled | RegexOptions.Multiline);
        #endregion

        // // type
        public static TextMarker ScanComment2(string in_txt, int start)
        {
            int length = in_txt.Length;
            int p = start + 1;

            while (p < length)
            {
                if (in_txt[p] == '\r' || in_txt[p] == '\n')
                    return new TextMarker(start - 1, p - start + 1, E_TextMarkerType.Comment);
                p++;
            }

            return null;
        }

        public static TextMarker ScanBracket3(string in_txt, int start)
        {
            int length = in_txt.Length;
            int p = start;
            char[] scan = new char[] { '{', '}' };

            while (p < length)
            {
                TextMarker m = ScanChars(in_txt, p, scan, E_TextMarkerType.Bracket3);
                if (m == null)
                    return null;
                else
                {
                    p = m.Index + m.Length - 1;
                    if (in_txt[p] == '{')
                    {
                        m = ScanBracket3(in_txt, p);
                        if (m == null)
                            return null;
                        else
                            p = m.Index + m.Length;
                    }
                    else if (in_txt[p] == '}')
                    {
                        return new TextMarker(start, m.Index + m.Length - start, E_TextMarkerType.Bracket3);
                    }
                    else
                        return null;
                }
            }

            return null;
        }

        public static TextMarker ScanChar(string in_txt, int start, char scan, E_TextMarkerType type)
        {
            int length = in_txt.Length;
            int p = start + 1;

            while (p < length)
            {
                if (in_txt[p] == scan)
                    return new TextMarker(start, p - start + 1, type);
                p++;
            }

            return null;
        }

        public static TextMarker ScanNextWord(string in_txt, int start)
        {
            int length = in_txt.Length;
            int p = start + 1;

            while (p < length)
            {
                char c = in_txt[p];
                if (Char.IsLetter(c) || Char.IsNumber(c) || c == '_')
                    return new TextMarker(start, p - start, E_TextMarkerType.Word);
                p++;
            }

            return null;
        }

        public static TextMarker ScanChars(string in_txt, int start, char[] scan, E_TextMarkerType type)
        {
            int length = in_txt.Length;
            int p = start + 1;

            while (p < length)
            {
                char c = in_txt[p];
                foreach (char s in scan)
                {
                    if (c == s)
                        return new TextMarker(start, p - start + 1, type);
                }
                p++;
            }

            return null;
        }

        public static TextMarker ScanToNonWord(string in_txt, int start)
        {
            int length = in_txt.Length;
            int p = start + 1;

            while (p < length)
            {
                char c = in_txt[p];
                if (!Char.IsLetter(c) && !Char.IsNumber(c) && c != '_')
                    return new TextMarker(start, p - start, E_TextMarkerType.Word);
                p++;
            }

            return null;
        }

        public int Index;
        public int Length;
        public E_TextMarkerType Type;
        public TextMarker(int in_index, int in_length, E_TextMarkerType in_type)
        {
            Index = in_index;
            Length = in_length;
            Type = in_type;
        }
        public string GetString(string in_txt)
        {
            string result = string.Empty;
            try
            {
                result = in_txt.Substring(Index, Length);
            }
            catch
            {
            }
            return result;
        }
        public int EndPos { get { return Index + Length - 1; } }
    }

    class TextMarkerList : List<TextMarker>
    {
        private string _text;
        public TextMarkerList(string in_text)
        {
            _text = in_text;
        }

        public string GetTextByMarker(TextMarker in_marker)
        {
            try
            {
                return _text.Substring(in_marker.Index, in_marker.Length);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }

}
