using RawParser.Model.FileHelper;
using RawParser.Model.ImageDisplay;
using RawParser.Model.Parser;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RawParser
{
    public partial class UI : Form
    {
        private RawImage currentRawImage { set; get; }
        public UI()
        {
            InitializeComponent();
        }

        private void UI_Load(object sender, EventArgs e)
        {
            try
            {
                //Set the drive display to read only
                this.drivelistbox.DropDownStyle = ComboBoxStyle.DropDownList;

                //Add the drive to the list
                FileChooser.DisplayDrive(this.drivelistbox);

                //set the first drive as the selected item
                this.drivelistbox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Exception occured");
                //Application.Exit();
            }
        }

        private void drivelistbox_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                ComboBox box = (ComboBox)sender;
                FileChooser.addRootPath(this.fileView, (string)box.SelectedItem);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message + ex.StackTrace, "Exception occured");
                //Application.Exit();
            }
        }

        private void fileView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            try
            {
                FileChooser.addSubfolder(e.Node);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message + ex.StackTrace, "Exception occured");
            }
        }

        private void fileView_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            //if tag is null, it's a file
            if (e.Node.Tag == null)
            {
                try
                {
                    //Open the file with the correct parser
                    string fileExension = FileChooser.getExtension(e.Node.FullPath);
                    Parser parser;
                    switch (fileExension)
                    {
                        case "NEF": parser = new NEFParser();
                            break;
                        case "DNG": parser = new DNGParser();
                            break;
                        default: throw new Exception("File not supported");//todo change exception types
                    }
                    this.currentRawImage = parser.parse(e.Node.FullPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message + ex.StackTrace, "Error");
                }
            }
        }

        private void groupBox3_Enter(object sender, EventArgs e)
        {

        }

        private void groupBox5_Enter(object sender, EventArgs e)
        {

        }

        private void fileView_AfterSelect(object sender, TreeViewEventArgs e)
        {

        }

        private void splitContainer2_SplitterMoved(object sender, SplitterEventArgs e)
        {

        }

        private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
        {

        }

        private void groupBox2_Enter(object sender, EventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private void groupBox4_Enter(object sender, EventArgs e)
        {

        }

        private void folderBrowserDialog1_HelpRequest(object sender, EventArgs e)
        {

        }
    }
}