using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using WXML.Model;
using WXML.Model.Database.Providers;
using WXML.Model.Descriptors;
using WXML.SourceConnector;

namespace WXMLDatabase
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Worm xml schema generator. v0.3 2007");
            if (args.Length == 0)
            {
                ShowUsage();
                return;
            }

            CommandLine.Utility.Arguments param = new CommandLine.Utility.Arguments(args);

            string server = null;
            if (!param.TryGetParam("S", out server))
            {
                server = "(local)";
            }

            string dbName = null;
            if (!param.TryGetParam("D", out dbName))
            {
                if (!File.Exists(server))
                {
                    Console.WriteLine("Database is not specified or file {0} not found", server);
                    ShowUsage();
                    return;
                }
            }

            string e;
            string user = null;
            string psw = null;
            if (!param.TryGetParam("E", out e))
            {
                e = "false";
                bool showUser = true;
                if (!param.TryGetParam("U", out user))
                {
                    Console.Write("User: ");
                    //ConsoleColor c = Console.ForegroundColor;
                    //Console.ForegroundColor = Console.BackgroundColor;
                    user = Console.ReadLine();
                    //Console.ForegroundColor = c;
                    showUser = false;
                }

                if (!param.TryGetParam("P", out psw))
                {
                    if (showUser)
                    {
                        Console.WriteLine("User: " + user);
                    }
                    Console.Write("Password: ");
                    ConsoleColor c = Console.ForegroundColor;
                    Console.ForegroundColor = Console.BackgroundColor;
                    psw = Console.ReadLine();
                    Console.ForegroundColor = c;
                }
            }
            bool i = bool.Parse(e);

            string schemas = null;
            param.TryGetParam("schemas", out schemas);

            string namelike = null;
            param.TryGetParam("name", out namelike);

            string file = null;
            if (!param.TryGetParam("O", out file))
            {
                file = dbName + ".xml";
            }

            string merge = null;
            if (!param.TryGetParam("F", out merge))
            {
                merge = "merge";
            }
            switch (merge)
            {
                case "merge":
                case "error":
                    //do nothing
                    break;
                default:
                    Console.WriteLine("Invalid \"Existing file behavior\" parameter.");
                    ShowUsage();
                    return;
            }

            string drop = null;
            if (!param.TryGetParam("R", out drop))
                drop = "false";
            bool dr = bool.Parse(drop);

            string namesp = dbName;
            param.TryGetParam("N", out namesp);

            string u = "true";
            if (!param.TryGetParam("Y", out u))
                u = "false";
            bool unify = bool.Parse(u);

            string hi = "true";
            if (!param.TryGetParam("H", out hi))
                hi = "false";
            bool hie = bool.Parse(hi);

            string tr = "true";
            if (!param.TryGetParam("T", out tr))
                tr = "false";
            bool transform = bool.Parse(tr);

            string es = "true";
            if (!param.TryGetParam("ES", out es))
                es = "false";
            bool escapeTables = bool.Parse(es);

            string ec = "true";
            if (!param.TryGetParam("EC", out ec))
                ec = "false";
            bool escapeColumns = bool.Parse(ec);

            string cn = "true";
            if (!param.TryGetParam("CN", out cn))
                cn = "false";
            bool capitalize = bool.Parse(cn);

            string ss = "false";
            if (!param.TryGetParam("SS", out ss))
                ss = "false";
            bool showStatement = bool.Parse(ss);

            string v = "1";
            if (!param.TryGetParam("V", out v))
                v = "1";

            DatabaseProvider dp = null;
            string m = null;
            if (!param.TryGetParam("M", out m))
            {
                m = "msft";
            }

            switch (m)
            {
                case "msft":
                case "mssql":
                    if (i)
                        dp = new MSSQLProvider(server, dbName);
                    else
                        dp = new MSSQLProvider(server, dbName, user, psw);

                    break;
                case "sqlce":
                    dp = new SQLCEProvider(server, psw);

                    break;
                case "mysql":
                    if (i)
                        dp = new MySQLProvider(server, dbName);
                    else
                        dp = new MySQLProvider(server, dbName, user, psw);

                    break;
                default:
                    Console.WriteLine("Invalid manufacturer parameter.");
                    ShowUsage();
                    return;
            }

            dp.OnDatabaseConnecting += (sender, conn) => Console.WriteLine("Connecting to \"{0}\"...", FilterPsw(conn));
            dp.OnStartLoadDatabase += (sender, cmd) => Console.WriteLine("Retriving tables via {0}...", showStatement ? cmd : string.Empty);

            SourceView db = dp.GetSourceView(schemas, namelike, escapeTables, escapeColumns);

            WXMLModel model = null;

            if (File.Exists(file))
            {
                if (merge == "error")
                {
                    Console.WriteLine("The file " + file + " is already exists.");
                    ShowUsage();
                    return;
                }
                Console.WriteLine("Loading file " + file);
                model = WXMLModel.LoadFromXml(new System.Xml.XmlTextReader(file));
            }
            else
            {
                model = new WXMLModel();
                model.Namespace = namesp;
                model.SchemaVersion = v;
                if (!Path.IsPathRooted(file))
                    file = Path.Combine(Directory.GetCurrentDirectory(), file);
                //File.Create(file);
            }

            SourceToModelConnector g = new SourceToModelConnector(db, model);

            Console.WriteLine("Generating xml...");
            HashSet<EntityDefinition> ents = new HashSet<EntityDefinition>();
            g.OnEntityCreated += (sender, entity) =>
            {
                Console.WriteLine("Create class {0} ({1})", entity.Name, entity.Identifier);
                ents.Add(entity);
            };

            g.OnPropertyCreated += (sender, prop, created) =>
            {
                EntityDefinition entity = prop.Entity;
                if (!ents.Contains(entity))
                {
                    ents.Add(entity);
                    Console.WriteLine("Alter class {0} ({1})", entity.Name, entity.Identifier);
                }
                Console.WriteLine("\t{1} property {2}.{0}", prop.Name, created ? "Add" : "Alter", entity.Name);
            };

            g.OnPropertyRemoved += (sender, prop) => Console.WriteLine("Remove: {0}.{1}", prop.Entity.Name, prop.Name);

            g.ApplySourceViewToModel(dr, 
                hie ? relation1to1.Hierarchy : unify ? relation1to1.Unify : relation1to1.Default, 
                transform,
                capitalize,
                dp.CaseSensitive);

            using (System.Xml.XmlTextWriter writer = new System.Xml.XmlTextWriter(file, Encoding.UTF8))
            {
                writer.Formatting = System.Xml.Formatting.Indented;
                model.GetXmlDocument().Save(writer);
            }

            Console.WriteLine("Done!");
            //Console.ReadKey();
        }

        private static string FilterPsw(string conn)
        {
            int pos = conn.IndexOf("Password=", StringComparison.InvariantCultureIgnoreCase);
            if (pos >= 0)
            {
                int e = conn.IndexOf(';', pos);
                if (e < 0) e = conn.Length;

                pos += 9;

                string psw = new string('*', e - pos);

                conn = conn.Remove(pos, e - pos).Insert(pos, psw);
            }
            return conn;
        }

        static void ShowUsage()
        {
            Console.WriteLine("Command line parameters");
            Console.WriteLine("  -O=value\t-  Output file name.\n\t" + 
"Example: -O=test.xml. Default is <database>.xml\n");
            Console.WriteLine("  -S=value\t-  Database server.\n\t"+
"Example: -S=(local). Default is (local).\n");
            Console.WriteLine("  -E\t\t-  Integrated security.\n");
            Console.WriteLine("  -U=value\t-  Username\n");
            Console.WriteLine("  -P=value\t-  Password. Will requested if need.\n");
            Console.WriteLine("  -D=value\t-  Initial catalog(database). Example: -D=test\n");
            Console.WriteLine("  -M=[msft|mysql]\t-  Manufacturer. Example: -M=msft. Default is msft.\n");
            Console.WriteLine("  -schemas=list\t-  Database schema filter.\n\t" + 
"Example: -schemas=dbo,one\n\tInclude all tables in schemas dbo and one\n\t" +
"Example: -schemas=(dbo,one)\n\tInclude all tables in all schemas except dbo and one\n");
            Console.WriteLine("  -name=value\t-  Database table name filter.\n\t"+
"Example: -name=aspnet_%,tbl%\n\tInclude all tables starts with aspnet_ or tbl\n\t" +
"Example: -name=(aspnet_%)\n\tInclude all tables except whose who starts with aspnet_\n");
            Console.WriteLine("  -F=[error|merge]\t-  Existing file behavior.\n\t" +
"Example: -F=error. Default is merge.\n");
            Console.WriteLine("  -R\t\t-  Drop deleted columns. Meaningfull only with merge behavior.\n");
            Console.WriteLine("  -N=value\t-  Objects namespace. Example: -N=test.\n");
            Console.WriteLine("  -Y\t\t-  Unify entities with the same PK(1-1 relation). Example: -Y.\n");
            Console.WriteLine("  -H\t\t-  Make hierarchy from 1-1 relations. Example: -H.\n");
            Console.WriteLine("  -T\t\t-  Transform property names. Example: -T. "+
"Removes id, _id, _dt postfix and other cleanup processing\n");
            Console.WriteLine("  -ES\t\t-  Escape table names. Example: -ES. Default is true.\n");
            Console.WriteLine("  -EC\t\t-  Escape column names. Example: -EC. Default is true.\n");
            Console.WriteLine("  -CN\t\t-  Capitalize names. Example: -CN. Default is true. "+
"Linq never capitalize names so this option for linq compatibility. Column [name] will be a property called Name (in linq - name).");
            Console.WriteLine("  -SS\t\t-  Show query schema statement. Example: -SS. Default is false\n");
            Console.WriteLine("  -V=value\t-  Schema version. Example: -V=1. Default is 1\n");
        }
    }
}
