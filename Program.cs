using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

using System.CodeDom;
using System.CodeDom.Compiler;

namespace idl2cs
{
    enum E_TextMarkerType
    {
        None,
        Comment,
        Attr,
        Para,
        Bracket1,
        Bracket2,
        Bracket3,
        Word,
    }

    enum E_ParseStatus
    {
        None,
        WaitAttrTitle,
    }

    class BaseScanner
    {
        protected int p;
        protected string txt;

        protected virtual void OnMarker(TextMarker m)
        {
            if (m != null)
                p = m.EndPos + 1;
        }

        public BaseScanner(string in_txt)
        {
            txt = in_txt;
        }

        public void Scan()
        {
            p = 0;
            int len = txt.Length;
            TextMarker m = null;
            while (p < len)
            {
                char c = txt[p];
                if (c == '/')
                {
                    if (p + 1 < txt.Length)
                    {
                        char c2 = txt[p + 1];
                        if (c2 == '*')
                            m = TextMarker.ScanComment1(txt, p + 1);
                        else if (c2 == '/')
                            m = TextMarker.ScanComment2(txt, p + 1);
                        else
                            p++;
                    }
                }
                else if (Char.IsLetter(c) || Char.IsNumber(c) || c == '_')
                {
                    m = TextMarker.ScanToNonWord(txt, p);
                }
                else if (c == '[')
                {
                    m = TextMarker.ScanChar(txt, p, ']', E_TextMarkerType.Bracket2);
                }
                else if (c == '(')
                {
                    m = TextMarker.ScanChar(txt, p, ')', E_TextMarkerType.Bracket1);
                }
                else if (c == '{')
                {
                    m = TextMarker.ScanBracket3(txt, p);
                }
                else
                    p++;

                if (m != null)
                {
                    OnMarker(m);
                    m = null;
                }
            }
        }

    }

    class LibraryScanner : BaseScanner
    {
        E_ParseStatus status = E_ParseStatus.None;
        TextMarker attr = null;
        CodeCompileUnit r = new CodeCompileUnit();
        CodeNamespace code;

        public CodeCompileUnit Result { get { return r; } }

        public LibraryScanner(string attr, string name, string block)
            : base(block)
        {
            txt = txt.Substring(1, txt.Length - 2);

            CodeNamespace import = new CodeNamespace();
            import.Imports.Add(new CodeNamespaceImport("System"));
            import.Imports.Add(new CodeNamespaceImport("System.Runtime.InteropServices"));
            r.Namespaces.Add(import);

            code = new CodeNamespace(name);
            r.Namespaces.Add(code);

            Match lib_help_string = TextMarker.find_helpstring.Match(attr);
            if (lib_help_string.Success)
                code.Comments.Add(new CodeCommentStatement(lib_help_string.Groups["value"].Value));
        }

        protected override void OnMarker(TextMarker m)
        {
            base.OnMarker(m);

            TextMarker tm = null;

            switch (m.Type)
            {
                case E_TextMarkerType.None:
                    break;
                case E_TextMarkerType.Comment:
                    break;
                case E_TextMarkerType.Attr:
                    attr = m;
                    status = E_ParseStatus.WaitAttrTitle;
                    break;
                case E_TextMarkerType.Para:
                    break;
                case E_TextMarkerType.Bracket1:
                    break;
                case E_TextMarkerType.Bracket2:
                    break;
                case E_TextMarkerType.Bracket3:
                    break;
                case E_TextMarkerType.Word:
                    string cmd = m.GetString(txt);
                    
                    if (status == E_ParseStatus.WaitAttrTitle)
                    {
                    }
                    else if (status == E_ParseStatus.None)
                    {
                        if (cmd == "import")
                        {
                            tm = TextMarker.ScanChar(txt, p, ';', E_TextMarkerType.None);
                            p = tm.EndPos;
                        }
                        else if (cmd == "typedef")
                        {
                            tm = TextMarker.ScanChar(txt, p, ';', E_TextMarkerType.None);
                            string tmp = tm.GetString(txt);
                            p = tm.EndPos;
                        }
                        else if (cmd == "cpp_quote")
                        {
                            tm = TextMarker.ScanChar(txt, p, ';', E_TextMarkerType.None);
                            p = tm.EndPos;
                        }
                        else
                        {
                            Console.WriteLine(cmd);
                        }
                    }
                    break;
                default:
                    break;
            }
        }
    }

    class BasicScanner : BaseScanner
    {
        E_ParseStatus status = E_ParseStatus.None;
        TextMarker attr_block;
        Dictionary<CodeCompileUnit, string> code_results = new Dictionary<CodeCompileUnit, string>();

        public Dictionary<CodeCompileUnit, string> CodeResults { get { return code_results; } }

        public BasicScanner(string in_txt)
            : base(in_txt)
        {
        }

        protected override void OnMarker(TextMarker m)
        {
            // advnce to marker end
            base.OnMarker(m);

            TextMarker tm;

            switch (m.Type)
            {
                case E_TextMarkerType.Attr:
                    break;
                case E_TextMarkerType.Para:
                    break;
                case E_TextMarkerType.Bracket1:
                    break;
                case E_TextMarkerType.Bracket2:
                    status = E_ParseStatus.WaitAttrTitle;
                    attr_block = m;
                    break;
                case E_TextMarkerType.Bracket3:
                    break;
                case E_TextMarkerType.Word:
                    string cmd = m.GetString(txt);

                    if (status == E_ParseStatus.WaitAttrTitle)
                    {
                        if (cmd == "library")
                        {
                            TextMarker library_block = null;

                            library_block = TextMarker.ScanNextWord(txt, p);
                            if (library_block == null)
                                throw new Exception("Fail parse library block.");

                            library_block = TextMarker.ScanToNonWord(txt, library_block.EndPos);
                            if (library_block == null)
                                throw new Exception("Fail parse library block.");

                            string lib_name = library_block.GetString(txt);

                            library_block = TextMarker.ScanChar(txt, p, '{', E_TextMarkerType.None);
                            if (library_block == null)
                                throw new Exception("Fail parse library block.");

                            library_block = TextMarker.ScanBracket3(txt, library_block.EndPos);
                            if (library_block == null)
                                throw new Exception("Fail parse library block.");

                            CodeCompileUnit cu = ProcessLibraryBlock(attr_block.GetString(txt), lib_name, library_block.GetString(txt));

                            if (cu != null)
                                code_results.Add(cu, lib_name);

                            status = E_ParseStatus.None;
                            attr_block = null;
                            p = library_block.EndPos;
                        }
                    }
                    else if (status == E_ParseStatus.None)
                    {
                        if (cmd == "import")
                        {
                            tm = TextMarker.ScanChar(txt, p, ';', E_TextMarkerType.None);
                            string tmp = tm.GetString(txt);
                            p = tm.Index + tm.Length;
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        private CodeCompileUnit ProcessLibraryBlock(string attr, string name, string block)
        {
            LibraryScanner scan = new LibraryScanner(attr, name, block);
            scan.Scan();
            return scan.Result;
        }
    }


    class Program
    {


        static E_ParseStatus status = E_ParseStatus.None;
        static TextMarker attr_block = null;
        static Dictionary<CodeCompileUnit, string> code_unit_list = new Dictionary<CodeCompileUnit, string>();

        static bool ProcessInclude(ref string org_txt)
        {
            Match m = TextMarker.find_include.Match(org_txt);
            if (m.Success)
            {
                try
                {
                    string inc = File.ReadAllText(m.Groups["filename"].Value);
                    if (!string.IsNullOrEmpty(inc))
                    {
                        while (ProcessInclude(ref inc)) { }
                        org_txt = org_txt.Remove(m.Captures[0].Index, m.Captures[0].Length).Insert(m.Captures[0].Index, inc);
                    }
                }
                catch (Exception)
                {
                    org_txt = org_txt.Remove(m.Captures[0].Index, m.Captures[0].Length);
                }
            }

            return m.Success;
        }

        static void ProcessIncludeLoop(ref string org_txt)
        {
            while (ProcessInclude(ref org_txt)) { }
        }

        static void RemoveWholeLineComment(ref string org_txt)
        {
            org_txt = TextMarker.find_whole_line_comment.Replace(org_txt, "");
        }

        static Dictionary<string, int> PrepareCommandDic()
        {
            Dictionary<string, int> result = new Dictionary<string, int>();

            result.Add("import", 0);
            result.Add("interface", 0);
            result.Add("library", 0);
            result.Add("typedef", 0);
            result.Add("enum", 0);

            return result;
        }

        static int OnMarker(string txt, TextMarker m)
        {
            int p = m.EndPos;
            switch (m.Type)
            {
                case E_TextMarkerType.Attr:
                    break;
                case E_TextMarkerType.Para:
                    break;
                case E_TextMarkerType.Bracket1:
                    break;
                case E_TextMarkerType.Bracket2:
                    status = E_ParseStatus.WaitAttrTitle;
                    attr_block = m;
                    break;
                case E_TextMarkerType.Bracket3:
                    break;
                case E_TextMarkerType.Word:
                    string cmd = m.GetString(txt);

                    if (status == E_ParseStatus.WaitAttrTitle)
                    {
                        if (cmd == "library")
                        {
                            TextMarker library_block = null;

                            library_block = TextMarker.ScanNextWord(txt, p);
                            if (library_block == null)
                                throw new Exception("Fail parse library block."); 

                            library_block = TextMarker.ScanToNonWord(txt, library_block.EndPos);
                            if (library_block == null)
                                throw new Exception("Fail parse library block.");

                            string lib_name = library_block.GetString(txt);

                            library_block = TextMarker.ScanChar(txt, p, '{', E_TextMarkerType.None);
                            if (library_block == null)
                                throw new Exception("Fail parse library block."); 

                            library_block = TextMarker.ScanBracket3(txt, library_block.EndPos);
                            if (library_block == null)
                                throw new Exception("Fail parse library block."); 

                            CodeCompileUnit cu = ProcessLibraryBlock(attr_block.GetString(txt), lib_name, library_block.GetString(txt));

                            if (cu != null)
                                code_unit_list.Add(cu, lib_name);

                            status = E_ParseStatus.None;
                            attr_block = null;
                            p = library_block.EndPos;
                        }
                    }
                    else if (status == E_ParseStatus.None)
                    {
                        if (cmd == "import")
                        {
                            m = TextMarker.ScanChar(txt, p, ';', E_TextMarkerType.None);
                            p = m.Index + m.Length;
                        }
                    }
                    break;
                default:
                    break;
            }

            return p;
        }

        static void DoConvert(string in_filename)
        {
            if (!File.Exists(in_filename))
            {
                Console.WriteLine("{0} not exists.", in_filename);
                return;
            }

            string out_filename = Path.ChangeExtension(in_filename, ".cs");
            try
            {
                string idl = File.ReadAllText(in_filename);
                while (ProcessInclude(ref idl)) { }

                BasicScanner scan = new BasicScanner(idl);
                scan.Scan();

                foreach (KeyValuePair<CodeCompileUnit, string> pair in code_unit_list)
                {
                    WriteCode(pair.Key, Path.Combine(Path.GetDirectoryName(out_filename), pair.Value + ".cs"));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }
            finally
            {
            }
        }

        private static bool WriteCode(CodeCompileUnit unit, string filename)
        {
            try
            {
                StreamWriter code_writer = new StreamWriter(filename);

                Microsoft.CSharp.CSharpCodeProvider code_provider = new Microsoft.CSharp.CSharpCodeProvider();
                CodeGeneratorOptions code_opts = new CodeGeneratorOptions();
                code_opts.BlankLinesBetweenMembers = false;
                code_opts.VerbatimOrder = true;
                code_opts.BracingStyle = "C";
                code_provider.GenerateCodeFromCompileUnit(unit, code_writer, code_opts);
                code_writer.Close();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static CodeCompileUnit ProcessLibraryBlock(string attr, string cmd, string block)
        {
            CodeCompileUnit r = new CodeCompileUnit();
            CodeNamespace import = new CodeNamespace();
            import.Imports.Add(new CodeNamespaceImport("System"));
            import.Imports.Add(new CodeNamespaceImport("System.Runtime.InteropServices"));
            r.Namespaces.Add(import);

            CodeNamespace code = new CodeNamespace(cmd);
            r.Namespaces.Add(code);

            Match lib_help_string = TextMarker.find_helpstring.Match(attr);
            if (lib_help_string.Success)
                code.Comments.Add(new CodeCommentStatement(lib_help_string.Groups["value"].Value));

            return r;
        }

        static Dictionary<string, int> dic_command = PrepareCommandDic();

        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Usage:\n\t{0} <idl file>", Path.GetFileName(Environment.GetCommandLineArgs()[0]));
                return;
            }

            DoConvert(args[0]);
        }

        
    }
}

//static void MainOld(string[] args)
//        {
//            if (args.Length != 1)
//            {
//                Console.WriteLine("Usage:\n\t{0} <idl file>", Path.GetFileName(Environment.GetCommandLineArgs()[0]));
//                return;
//            }

//            string in_filename = args[0];
//            if (!File.Exists(in_filename))
//            {
//                Console.WriteLine("{0} not exists.", in_filename);
//                return;
//            }
//            string out_filename = Path.ChangeExtension(in_filename, ".cs");

//            try
//            {
//                string idl = File.ReadAllText(in_filename);
//                if (idl == null || idl.Length == 0)
//                {
//                    Console.WriteLine("Fail reading input file.");
//                    return;
//                }

//                MatchCollection r;
//                #region Process include
//                while (ProcessInclude(ref idl)) { }
//                #endregion
//                #region Prepare mapping hash table
//                Dictionary<string, string> type_def_table = new Dictionary<string, string>();
//                Dictionary<string, string> type_mapping_table = new Dictionary<string, string>();
//                type_mapping_table.Add("int", "int");
//                type_mapping_table.Add("short", "short");
//                type_mapping_table.Add("long", "int");
//                type_mapping_table.Add("double", "double");
//                type_mapping_table.Add("LONGLONG", "Int64");
//                type_mapping_table.Add("unsigned long", "uint");
//                type_mapping_table.Add("unsigned char", "byte");
//                type_mapping_table.Add("void", "IntPtr");
//                type_mapping_table.Add("HRESULT", "int");
//                type_mapping_table.Add("BOOL", "bool");
//                type_mapping_table.Add("bool", "bool");
//                type_mapping_table.Add("BSTR", "string");
//                type_mapping_table.Add("interface", "interface");
//                type_mapping_table.Add("enum", "enum");
//                #endregion

//                StreamWriter w = new StreamWriter(out_filename, false);
//                w.WriteLine("// Auto generated by idl2cs /(c) toki, 2009/ @ {0}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
//                w.WriteLine("using System;");
//                w.WriteLine("using System.Collections.Generic;");
//                w.WriteLine("using System.Text;");
//                w.WriteLine("using System.Runtime.InteropServices;");
//                w.WriteLine("");
//                w.WriteLine("namespace {0}\n{{", Path.GetFileNameWithoutExtension(in_filename));


//#if USE_DOM
//                StreamWriter code_writer = new StreamWriter(Path.ChangeExtension(out_filename, ".code.cs"), false);
//                CodeCompileUnit code_root = new CodeCompileUnit();
//                CodeNamespace code_imports = new CodeNamespace();
//                code_root.Namespaces.Add(code_imports);
//                code_imports.Comments.Add(new CodeCommentStatement(String.Format("Auto generated by idl2cs /(c) toki, 2009/ @ {0}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"))));
//                code_imports.Imports.Add(new CodeNamespaceImport("System"));
//                code_imports.Imports.Add(new CodeNamespaceImport("System.Collections.Generic"));
//                code_imports.Imports.Add(new CodeNamespaceImport("System.Text"));
//                code_imports.Imports.Add(new CodeNamespaceImport("System.Runtime.InteropServices"));
//                CodeNamespace code = new CodeNamespace(Path.GetFileNameWithoutExtension(in_filename));
//                code_root.Namespaces.Add(code);

//                CodeTypeDeclaration test_enum = new CodeTypeDeclaration("BMDTestEnum");
//                test_enum.IsEnum = true;
//                CodeMemberField mt = new CodeMemberField("", "TEST03");
//                mt.InitExpression = new CodePrimitiveExpression(0xFFFF);
//                test_enum.Members.Add(new CodeMemberField("", "TEST01"));
//                test_enum.Members.Add(new CodeMemberField("", "TEST02"));
//                test_enum.Members.Add(mt);

//                code.Types.Add(test_enum);
//#endif                
//                r = null;

//                #region typedef
//                Console.WriteLine("Process typedef ...");
//                r = find_typedef.Matches(idl);

//                foreach (Match m in r)
//                {
//                    try
//                    {
//                        string key = m.Groups["key"].Value;
//                        string value = m.Groups["value"].Value;
//                        //Console.WriteLine("Adding: {0} => {1}", key, value);

//                        if (type_def_table.ContainsKey(key))
//                        {
//                            if (value.StartsWith("enum "))
//                            {
//                                type_def_table.Remove(key);
//                                type_def_table.Add(key, "enum");
//                                type_def_table.Add(value.Substring(5).Trim(), key);
//                            }
//                        }
//                        else
//                        {
//                            type_def_table.Add(m.Groups["key"].Value, m.Groups["value"].Value);
//                        }
//                    }
//                    catch (Exception)
//                    {
//                        Console.WriteLine("Error when add: {0} => {1}", m.Groups["key"].Value, m.Groups["value"].Value);
//                    }
//                }

//                foreach (KeyValuePair<string, string> p in type_def_table)
//                {
//                    Console.WriteLine("\t{0} => {1}", p.Key, p.Value);
//                }
//                Console.WriteLine("Total {0} typedef processed.", r.Count);
//                #endregion

//                #region enum
//                Console.WriteLine("Process enum ...");
//                r = find_enum.Matches(idl);

//                w.Write("#region enum");
//                foreach (Match m in r)
//                {
//                    string enum_name = m.Groups["name2"].Value.Trim() == String.Empty ? m.Groups["name1"].Value.Trim() : m.Groups["name2"].Value.Trim();
//                    string def_name;
//                    type_def_table.TryGetValue(enum_name, out def_name);

//                    if (!String.IsNullOrEmpty(def_name))
//                        enum_name = def_name;

//                    Console.WriteLine("\t{0}", enum_name);

//                    w.WriteLine();
//                    if (enum_name.EndsWith("s"))
//                    {
//                        w.WriteLine("// idl2cs warning: possible flag enum\n[Flags]");
//                    }
//                    w.WriteLine("public enum {0}\n{{{1}}}", enum_name, m.Groups["body"].Value);
//                    if (!type_def_table.ContainsKey(enum_name))
//                        type_def_table.Add(enum_name, "enum");
//                }
//                w.WriteLine("#endregion");
//                Console.WriteLine("Total {0} enum processed.", r.Count);
//                #endregion

//                #region coclass
//                Console.WriteLine("Process coclass ...");
//                r = find_coclass.Matches(idl);
//                w.Write("\n#region coclass");

//                foreach (Match m in r)
//                {
//                    w.WriteLine();
//                    string cc_name = m.Groups["name"].Value.Trim();
//                    string cc_attr = m.Groups["attr"].Value.Trim();
//                    string cc_declear = m.Groups["declear"].Value.Trim();
//                    string cc_uuid = string.Empty;

//                    Console.WriteLine("\tProcess coclass {0}", cc_name);

//                    Match mm = null;
//                    mm = find_helpstring.Match(cc_attr);
//                    if (mm.Success)
//                        w.WriteLine("/// <summary>\n/// {0}\n/// </summary>", mm.Groups["value"].Value);

//                    mm = null;
//                    mm = find_uuid.Match(cc_attr);
//                    if (mm.Success)
//                    {
//                        cc_uuid = mm.Groups["value"].Value;
//                        w.WriteLine(@"[ComImport, Guid(""{0}"")]", cc_uuid);
//                    }

//                    w.WriteLine("public class {0}\n{{\n/*\nidl2cs warning: edit this block for helper\n{1}\n*/\n}}", cc_name, cc_declear);

//                    idl = idl.Remove(m.Captures[0].Index, m.Captures[0].Length);
//                }

//                w.WriteLine("#endregion");
//                Console.WriteLine("Total {0} coclass processed.", r.Count);
//                #endregion

//                #region pre-scan interrface
//                Console.WriteLine("Pre-scan interface ...");
//                r = find_fwd_interface.Matches(idl);
//                foreach (Match m in r)
//                {
//                    string interface_name = m.Groups["name"].Value.Trim();

//                    if (!type_def_table.ContainsKey(interface_name))
//                    {
//                        type_def_table.Add(interface_name, "interface");
//                        Console.WriteLine("\tAdd {0} to def hash table.", interface_name);
//                    }
//                }
//                Console.WriteLine("Total {0} interface scanned.", r.Count);
//                #endregion

//                #region interface
//                Console.WriteLine("Process interface ...");
//                r = find_interface.Matches(idl);

//                w.Write("\n#region interface");
//                foreach (Match m in r)
//                {
//                    string interface_name = m.Groups["name"].Value.Trim();
//                    string interface_type = m.Groups["type"].Value.Trim();
//                    string interface_comtype = interface_type == "IDispatch" ? "InterfaceIsIDispatch" : "InterfaceIsIUnknown";
//                    string interface_attr = m.Groups["attr"].Value.Trim();
//                    Console.WriteLine("\tProcess interface: {0} => type: {1}", interface_name, interface_type);

//                    Match uuid;
//                    uuid = find_uuid.Match(interface_attr);
//                    Match helpstring;
//                    helpstring = find_helpstring.Match(interface_attr);

//                    w.WriteLine();
//                    w.WriteLine("/// <summary>\n/// {0}\n/// </summary>", helpstring.Groups["value"].Value);
//                    w.WriteLine(@"[ComImport, Guid(""{0}""), InterfaceType(ComInterfaceType.{1})]", uuid.Groups["value"].Value, interface_comtype);
//                    if (interface_type=="IUnknown" || interface_type == "IDispatch")
//                        w.WriteLine("public interface {0}\n{{", interface_name);
//                    else
//                        w.WriteLine("public interface {0} : {1}\n{{", interface_name, interface_type);

//                    if (!type_def_table.ContainsKey(interface_name))
//                        type_def_table.Add(interface_name, "interface");

//                    string interface_func = find_whole_line_comment.Replace(m.Groups["function"].Value, "");

//                    MatchCollection rr = find_function.Matches(interface_func);
//                    foreach (Match mm in rr)
//                    {
//                        string func_type = mm.Groups["func_type"].Value.Trim();
//                        string func_name = mm.Groups["func_name"].Value.Trim();
//                        //Console.WriteLine("\t{0} {1}", func_type, func_name);

//                        string func_native_type = string.Empty;
//                        if (!type_mapping_table.TryGetValue(func_type, out func_native_type))
//                            if (!type_def_table.TryGetValue(func_type, out func_native_type))
//                            {
//                                Console.WriteLine("[warning] unable to mapping {0} {1} to c# native type.", func_type, func_name);
//                                w.WriteLine("// idl2cs warning: unmapped type: {0}", func_type);
//                            }

//                        if (!type_mapping_table.ContainsValue(func_native_type))
//                        {
//                            if (!type_mapping_table.TryGetValue(func_native_type, out func_native_type))
//                            {
//                                Console.WriteLine("[warning] unable to mapping {0} {1} to c# native type.", func_native_type, func_name);
//                                w.WriteLine("// idl2cs warning: unmapped type: {0}", func_native_type);
//                            }
//                        }

//                        if (!string.IsNullOrEmpty(func_native_type) && func_native_type != "enum")
//                            func_type = func_native_type;

//                        string func_paras = string.Empty;

//                        if (mm.Groups["func_para"].Value.Trim() != "void")
//                        {
//                            string[] paras = mm.Groups["func_para"].Value.Split(new char[] { ',' });

//                            for (int i = 0; i < paras.Length; i++)
//                            {
//                                string pa = paras[i].Trim();
//                                string para_attr;

//                                //Console.WriteLine(pa);

//                                Match ma = find_func_attr.Match(pa.Trim());
//                                string[] pa_split = null;
//                                if (ma.Success)
//                                {
//                                    para_attr = ma.Groups["attr"].Value;
//                                    pa_split = ma.Groups["para"].Value.Trim().Split();
//                                }
//                                else
//                                {
//                                    para_attr = string.Empty;
//                                    pa_split = pa.Split();
//                                }

//                                if (pa_split.Length < 2)
//                                    continue;

//                                int j = pa_split.Length - 1;

//                                string para_type = string.Empty;
//                                string para_native_type = string.Empty;
//                                string para_name = pa_split[j];
//                                int para_ptr_count = 0;

//                                foreach (char c in para_name)
//                                {
//                                    if (c == '*')
//                                        para_ptr_count++;
//                                    else
//                                        break;
//                                }

//                                j--;
//                                para_type = pa_split[j];

//                                if (para_ptr_count != 0)
//                                {
//                                    para_name = para_name.Substring(para_ptr_count);
//                                }
//                                else
//                                {
//                                    for (int ii = para_type.Length - 1; ii != 0; ii--)
//                                    {
//                                        if (para_type[ii] == '*')
//                                            para_ptr_count++;
//                                        else
//                                            break;
//                                    }

//                                    if (para_ptr_count != 0)
//                                    {
//                                        para_type = para_type.Substring(0, para_type.Length-para_ptr_count);
//                                    }
//                                }

//                                j--;
//                                while (j >= 0)
//                                {
//                                    para_type = pa_split[j] + " " + para_type;
//                                    j--;
//                                }

//                                if (!type_mapping_table.TryGetValue(para_type, out para_native_type))
//                                    if (!type_def_table.TryGetValue(para_type, out para_native_type))
//                                    {
//                                        Console.WriteLine("[warning] unable to mapping {0} to c# natvie type.", para_type);
//                                        w.WriteLine("// idl2cs warning: unmapped type: {0}", para_type);
//                                    }

//                                if (!type_mapping_table.ContainsValue(para_native_type))
//                                {
//                                    if (!type_mapping_table.TryGetValue(para_native_type, out para_native_type))
//                                    {
//                                        Console.WriteLine("[warning] unable to mapping {0} to c# native type.", para_native_type);
//                                        w.WriteLine("// idl2cs warning: unmapped type: {0}", para_native_type);
//                                    }
//                                }

//                                string final_para = string.Empty;
//                                bool is_out = false;
//                                switch (para_attr)
//                                {
//                                    case "in":
//                                        final_para += "[In";
//                                        break;
//                                    case "out":
//                                        final_para += "[Out";
//                                        is_out = true;
//                                        break;
//                                    default:
//                                        break;
//                                }

//                                if (final_para != string.Empty)
//                                {
//                                    if (para_type == "BSTR")
//                                        final_para += ", MarshalAs(UnmanagedType.BStr)";

//                                    final_para += "] ";
//                                }

//                                switch (para_ptr_count)
//                                {
//                                    case 0:
//                                        goto normal_final_para_type;
//                                    case 1:
//                                        if (!is_out)
//                                        {
//                                            if (para_native_type == "interface")
//                                                final_para += para_type + " ";
//                                            else if (para_type == "void")
//                                                final_para += para_native_type + " ";
//                                            else
//                                                Console.WriteLine("[warning] {0}/{1}* with [In] attr in {2}:{3}",
//                                                              para_type, para_native_type,
//                                                              interface_name, func_name);

//                                            goto after_normal_final_para_type;
//                                        }
//                                        else
//                                        {
//                                            final_para += "out ";
//                                            goto normal_final_para_type;
//                                        }
//                                    case 2:
//                                        //if (is_out)
//                                        //{
//                                            if (para_native_type == "interface")
//                                            {
//                                                final_para += string.Format("out {0} ", para_type);
//                                                goto after_normal_final_para_type;
//                                            }
//                                            else if (para_type == "void")
//                                            {
//                                                final_para += string.Format("out {0} ", para_native_type);
//                                                goto after_normal_final_para_type;
//                                            }
//                                        //}
//                                        Console.WriteLine("[warning] {0}/{1}** in {2}:{3}",
//                                                      para_type, para_native_type,
//                                                      interface_name, func_name);
//                                        goto after_normal_final_para_type;
//                                    default:
//                                        break;
//                                }

//                            normal_final_para_type:
//                                if (para_native_type == "interface" || para_native_type == "enum")
//                                    final_para += para_type + " ";
//                                else
//                                    final_para += para_native_type + " ";

//                            after_normal_final_para_type:
//                                final_para += para_name;
//                                //Console.WriteLine(final_para);

//                                func_paras += final_para + ", ";
//                            }
//                        }

//                        if (!string.IsNullOrEmpty(func_paras))
//                            func_paras = func_paras.Substring(0, func_paras.Length - 2);

//                        w.WriteLine("\t[PreserveSig]\n\t{0} {1} ({2});", func_type, func_name, func_paras);
//                    }
//                    w.WriteLine("}}", interface_name);
//                }
//                w.WriteLine("#endregion");
//                Console.WriteLine("Total {0} interface processed.", r.Count);
//                #endregion

//                w.WriteLine("}");
//                w.Close();

//#if USE_DOM
//                Microsoft.CSharp.CSharpCodeProvider code_provider = new Microsoft.CSharp.CSharpCodeProvider();
//                CodeGeneratorOptions code_opts = new CodeGeneratorOptions();
//                code_opts.BlankLinesBetweenMembers = false;
//                code_opts.VerbatimOrder = true;
//                code_opts.BracingStyle = "C";
//                code_provider.GenerateCodeFromCompileUnit(code_root, code_writer, code_opts);
//                code_writer.Close(); 
//#endif
//            }
//            catch (Exception e)
//            {
//                Console.WriteLine(e);
//                return;
//            }

//#if AUTO_DUMP_OUTPUT
//            Console.WriteLine("\nDumping output file: {0}\n", out_filename);
//            StreamReader o = new StreamReader(out_filename);
//            while (!o.EndOfStream)
//            {
//                Console.WriteLine(o.ReadLine());
//            }
//            o.Close(); 
//#endif
//        }