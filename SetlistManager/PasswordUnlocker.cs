using System.Windows.Forms;

namespace SetlistManager
{
    public partial class PasswordUnlocker : Form
    {
        public PasswordUnlocker(string name = "")
        {
            InitializeComponent();
            txtPass.Text = name;
        }

        public string EnteredText
        {
            get
            {
                return (txtPass.Text);
            }
        }

        private void btnGo_Click(object sender, System.EventArgs e)
        {
            Close();
        }

        public void Renamer()
        {
            txtPass.PasswordChar = '\0';
            topLabel.Text = "Enter new name below\nthen click OK";
            toolTip1.SetToolTip(btnOK, "Click to change name");
            toolTip1.SetToolTip(txtPass, "Enter new name here");
        }
     }
}
