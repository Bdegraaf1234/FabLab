// <copyright file="Program.cs" company="Biomolecular Mass Spectrometry and Proteomics (http://hecklab.com)">
// This file is part of HeckLib.
//
// HeckLib is free software; you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation; either version 2.1 of the License, or
// (at your option) any later version.
//
// HeckLib is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with HeckLib; if not, write to the Free Software
// Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
// </copyright>

using System;
using System.Windows.Forms;

namespace FabLab
{
    /// <summary>
    /// Main WFA app class.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
			try
            {
				if (args.Length != 0)
				{
                    Application.Run(new FabLabForm(args[0]));
                }
                else
                    Application.Run(new FabLabForm());
            }
			catch (Exception er)
			{
                MessageBox.Show($"an error occured: {er.Message}");
            }
        }
    }
}
