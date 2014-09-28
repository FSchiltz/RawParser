using RawParser.Model.FileHelper;
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

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private void fileView_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Node.Tag == null)
            {
                MessageBox.Show(e.Node.Name, "Image cliked");
            }
        }
    }
}