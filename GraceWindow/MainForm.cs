using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Grace.Parsing;
using Grace.Runtime;
using Grace.Execution;
using Grace;
using System.IO;

namespace GraceWindow
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void runButton_Click(object sender, EventArgs e)
        {
            OutputSink sink = new TextboxSink(outputText);
            outputText.Text = "";
            var interp = new Interpreter(sink);
            interp.LoadPrelude();
            try
            {
                var p = new Parser(codeText.Text);
                var module = p.Parse();
                var ett = new ExecutionTreeTranslator();
                var eModule = ett.Translate(module as ObjectParseNode);
                try
                {
                    eModule.Evaluate(interp);
                }
                catch (GraceExceptionPacketException gep)
                {
                    sink.WriteLine("Uncaught exception:");
                    ErrorReporting.WriteException(gep.ExceptionPacket);
                    if (gep.ExceptionPacket.StackTrace != null)
                    {
                        foreach (var l in gep.ExceptionPacket.StackTrace)
                        {
                            sink.WriteLine("    from " + l);
                        }
                    }
                }
                catch (Exception ex)
                {
                    sink.WriteLine("=== A run-time error occurred: " + ex.Message + " ===");
                }
            }
            catch (StaticErrorException)
            {
                sink.WriteLine("=== A static error prevented the program from running. ===");
            }
            catch (Exception)
            {
                sink.WriteLine("=== An internal error occurred. ===");
            }

        }

        private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void loadButton_Click(object sender, EventArgs e)
        {
            openFile.DefaultExt = "grace";
            if (openFile.FileName == null || openFile.FileName == "")
                openFile.InitialDirectory = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            openFile.ShowDialog(this);
        }

        private void openFile_FileOk(object sender, CancelEventArgs e)
        {
            using (StreamReader reader = File.OpenText(openFile.FileName))
            {
                codeText.Text = reader.ReadToEnd();
            }
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            if (saveFile.FileName == null || saveFile.FileName == "")
            {
                saveFile.InitialDirectory = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (openFile.FileName != null && openFile.FileName != "")
                    saveFile.FileName = openFile.FileName;
            }
            saveFile.DefaultExt = "grace";
            saveFile.AddExtension = true;
            saveFile.ShowDialog();
        }

        private void saveFile_FileOk(object sender, CancelEventArgs e)
        {
            using (StreamWriter writer = new StreamWriter(File.OpenWrite(saveFile.FileName)))
            {
                writer.Write(codeText.Text);
            }
        }

        private void modulePathButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Imported modules are found under "
                + Path.Combine(
                    Path.Combine(Environment.GetFolderPath(
                            Environment.SpecialFolder.ApplicationData),
                        "grace"),
                    "modules"));
        }

        private void aboutButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Runtime revision: " + Interpreter.GetRuntimeVersion());
        }
    }

    class TextboxSink : OutputSink
    {
        TextBox box;
        public TextboxSink(TextBox box)
        {
            this.box = box;
        }

        public void WriteLine(string s)
        {
            box.Text += s + Environment.NewLine;
        }
    }
}
