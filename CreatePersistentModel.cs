using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UsingTostadoPersistentKit.TostadoPersistentKit
{
    public partial class CreatePersistentModel : Form
    {
        public CreatePersistentModel()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DefaultDatabaseCreator databaseCreator = new DefaultDatabaseCreator();

            databaseCreator.createPersistentDefaultModel();

            MessageBox.Show("Tables created succesfuly");
        }

        private void CreatePersistentModel_Load(object sender, EventArgs e)
        {

        }
    }
}
