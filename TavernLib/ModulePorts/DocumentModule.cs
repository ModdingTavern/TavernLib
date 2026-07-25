///////////////////////////////////////////////////
///         THIS IS A DECOMPILED SCRIPT         ///
///////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using Alta;
using Alta.Console;
using NLog;

namespace TavernLib.Debugging;

[Module("document", "Export all the commands to a document form")]
public class DocumentModule
{
    private const string FileName = "Documentation.zip";

    private static Logger logger = LogManager.GetCurrentClassLogger();

    private static XmlTextWriter writer;

    [Priority(1)]
    [Command(null, "Creates an HTML page of all commands")]
    private static FileDownload Document()
    {
        string path = FileDownload.GetPath("Command Documentation");
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            string text = Path.Combine(path, "documentation.zip");
            using (WebClient webClient = new WebClient())
            {
                webClient.DownloadFile("https://storage.googleapis.com/alta-common-storage/documentation.zip", text);
            }


            ZipFile.ExtractToDirectory(text, path);
            File.Delete(text);
        }

        string text2 = Path.Combine(path, "index.html");
        string text3 = Path.Combine(path, "..", "Documentation.zip");
        File.Delete(text2);
        File.Delete(text3);
        using (writer = new XmlTextWriter(text2, Encoding.Default))
        {
            writer.WriteDocType("html", "-//W3C//DTD XHTML 1.0 Transitional//EN", "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd", null);
            writer.WriteStartElement("html");
            writer.WriteAttributeString("lang", "en");
            Head();
            Body();
            writer.WriteEndElement();
        }
        
        ZipFile.CreateFromDirectory(path, text3);
        logger.Info("Successfully created a command document");
        Process.Start(text2);
        return "Documentation.zip";
    }

    private static void Head()
    {
        writer.WriteStartElement("head");
        Meta("charset", "utf-8");
        Meta("http-equiv", "X-UA-Compatible", "content", "IE=edge");
        Meta("name", "viewport", "content", "width=device-width, initial-scale=1, maximum-scale=1");
        Meta("name", "description", "content", "");
        Meta("name", "author", "content", "");
        Meta("name", "keywords", "content", "");
        writer.WriteElementString("title", "A Township Tale Commands");
        Link("shortcut icon", "image/x-icon", "images/favicon.ico");
        Link("stylesheet", "text/css", "fonts/font-awesome-4.3.0/css/font-awesome.min.css");
        Link("stylesheet", "text/css", "css/stroke.css");
        Link("stylesheet", "text/css", "css/bootstrap.css");
        Link("stylesheet", "text/css", "css/animate.css");
        Link("stylesheet", "text/css", "css/prettyPhoto.css");
        Link("stylesheet", "text/css", "css/style.css");
        Link("stylesheet", "text/css", "js/syntax-highlighter/styles/shCore.css", "all");
        Link("stylesheet", "text/css", "js/syntax-highlighter/styles/shThemeRDark.css", "all");
        Link("stylesheet", "text/css", "css/custom.css");
        writer.WriteEndElement();
    }

    private static void Meta(params string[] keyValues)
    {
        writer.WriteStartElement("meta");
        for (int i = 0; i < keyValues.Length; i += 2)
        {
            writer.WriteAttributeString(keyValues[i], keyValues[i + 1]);
        }

        writer.WriteEndElement();
    }

    private static void Link(string rel, string type, string href, string media = null)
    {
        writer.WriteStartElement("link");
        writer.WriteAttributeString("rel", rel);
        writer.WriteAttributeString("type", type);
        writer.WriteAttributeString("href", href);
        if (media != null)
        {
            writer.WriteAttributeString("media", media);
        }

        writer.WriteEndElement();
    }

    private static void Body()
    {
        writer.WriteStartElement("body");
        writer.WriteStartElement("div");
        writer.WriteAttributeString("id", "wrapper");
        ElementClassOpen("div", "container");
        TopSection();
        ElementClassOpen("div", "row");
        Navigation();
        Content();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        Scripts();
        writer.WriteEndElement();
    }

    private static void ElementClassOpen(string element, string className)
    {
        writer.WriteStartElement(element);
        writer.WriteAttributeString("class", className);
    }

    private static void TopSection()
    {
        ElementClassOpen("section", "section docs-heading");
        writer.WriteAttributeString("id", "top");
        ElementClassOpen("div", "row");
        ElementClassOpen("div", "col-md-12");
        ElementClassOpen("div", "big-title text-center");
        writer.WriteElementString("h1", "A Township Tale Commands");
        writer.WriteElementString("p", "Dynamically generated for version ");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteElementString("hr", "");
        writer.WriteEndElement();
    }

    private static void Navigation()
    {
        ElementClassOpen("div", "col-md-3");
        ElementClassOpen("nav", "docs-sidebar affix");
        writer.WriteAttributeString("data-spy", "affix");
        writer.WriteAttributeString("data-offset-top", "300");
        writer.WriteAttributeString("data-offset-bottom", "200");
        writer.WriteAttributeString("role", "navigation");
        ElementClassOpen("ul", "nav");
        foreach (Module module in CommandService.CommandCollection.Modules)
        {
            Navigation(module);
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void Navigation(Module module, string address = "#")
    {
        address += module.Name;
        writer.WriteStartElement("li");
        writer.WriteStartElement("a");
        writer.WriteAttributeString("href", address);
        writer.WriteString(module.Name);
        writer.WriteEndElement();
        ElementClassOpen("ul", "nav");
        address += "-";
        foreach (Command command in module.Commands)
        {
            Navigation(command, address);
        }

        foreach (Module submodule in module.Submodules)
        {
            Navigation(submodule, address);
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void Navigation(Command command, string address = "#")
    {
        address += command.Name;
        writer.WriteStartElement("li");
        writer.WriteStartElement("a");
        writer.WriteAttributeString("href", address);
        writer.WriteString(command.Name);
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void Content()
    {
        ElementClassOpen("div", "col-md-9");
        foreach (Module module in CommandService.CommandCollection.Modules)
        {
            Content(module);
        }

        writer.WriteEndElement();
    }

    private static void Content(Module module, string address = "")
    {
        address += module.Name;
        ElementClassOpen("section", "section");
        writer.WriteAttributeString("id", address);
        SectionHeading(module.Name);
        if (module.Aliases.Count > 1)
        {
            writer.WriteElementString("p", "Aka. " + GetAliases(module));
        }

        writer.WriteElementString("p", module.Description);
        address += "-";
        foreach (Command command in module.Commands)
        {
            Content(command, address);
        }

        writer.WriteEndElement();
        foreach (Module submodule in module.Submodules)
        {
            Content(submodule, address);
        }
    }

    private static void SectionHeading(string heading)
    {
        ElementClassOpen("div", "row");
        ElementClassOpen("div", "col-md-212 left-align");
        ElementClassOpen("h2", "dark-text");
        writer.WriteString(heading);
        writer.WriteStartElement("a");
        writer.WriteAttributeString("href", "#top");
        writer.WriteString("#back to top");
        writer.WriteEndElement();
        writer.WriteElementString("hr", "");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void Content(Command command, string address = "")
    {
        address += command.Name;
        ElementClassOpen("div", "row");
        ElementClassOpen("div", "col-md-12");
        writer.WriteStartElement("h4");
        writer.WriteAttributeString("id", address);
        writer.WriteString(command.Name);
        writer.WriteStartElement("i");
        writer.WriteString(" [ " + string.Join(", ", command.Parameters.Select((Parameter parameter) => parameter.Name)) + " ]");
        writer.WriteEndElement();
        writer.WriteEndElement();
        if (command.Aliases.Count > 1)
        {
            writer.WriteElementString("p", "Aka. " + GetAliases(command));
        }

        writer.WriteElementString("p", command.Description);
        writer.WriteStartElement("ul");
        foreach (Parameter parameter in command.Parameters)
        {
            writer.WriteStartElement("li");
            writer.WriteStartElement("b");
            writer.WriteString(parameter.Name);
            writer.WriteEndElement();
            if (parameter.HasDefault)
            {
                writer.WriteString(string.Concat(" (Default: ", parameter.Default, ")"));
            }

            writer.WriteString(" - " + (parameter.Description ?? "No description provided"));
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void Scripts()
    {
        Script("js/jquery.min.js");
        Script("js/bootstrap.min.js");
        Script("js/retina.js");
        Script("js/jquery.fitvids.js");
        Script("js/wow.js");
        Script("js/jquery.prettyPhoto.js");
        Script("js/custom.js");
        Script("js/main.js");
        Script("js/syntax-highlighter/scripts/shCore.js");
        Script("js/syntax-highlighter/scripts/shBrushXml.js");
        Script("js/syntax-highlighter/scripts/shBrushCss.js");
        Script("js/syntax-highlighter/scripts/shBrushJScript.js");
    }

    private static void Script(string src)
    {
        writer.WriteStartElement("script");
        writer.WriteAttributeString("src", src);
        writer.WriteString(" ");
        writer.WriteEndElement();
    }

    private static void Document(BasePart part, XmlTextWriter writer, int level, Action content)
    {
        writer.WriteStartElement("div");
        writer.WriteAttributeString("class", part.GetType().Name.ToLower());
        writer.WriteElementString("h" + (level + 1), part.Name);
        if (part is CommandPart commandPart && commandPart.Aliases.Count > 1)
        {
            writer.WriteElementString("h" + (level + 2), "Aka. " + GetAliases(commandPart));
        }

        writer.WriteStartElement("p");
        writer.WriteElementString("i", part.Description);
        writer.WriteEndElement();
        content?.Invoke();
        writer.WriteEndElement();
    }

    private static string GetAliases(CommandPart part)
    {
        return string.Join(", ", part.Aliases.ToArray(), 1, part.Aliases.Count - 1);
    }
}