using Bio.Extensions;
using BrightIdeasSoftware;
using hecklib.graphics.controls;
using HeckLib.chemistry;
using HeckLib.ConvenienceInterfaces.SpectrumMatch;
using HeckLib.objectlistview;
using HeckLib.utils;
using HeckLib.visualization;
using HeckLib.visualization.objectlistview;
using HeckLibWin32;
using HtmlGenerator;
using ImgtFilterLib;
using PsrmLib;
using PsrmLib.IO;
using PsrmLib.Models;
using PsrmLib.Processing;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace FabLab
{
	public partial class FabLabForm : Form
	{
		private readonly string LegendName = "Legend2";
		#region Data

		public Dictionary<((string, double[]), RegionType), char[]> GappedSequences = new Dictionary<((string, double[]), RegionType), char[]>();
		public Dictionary<string, List<List<Coverage>>> MultiScores { get => Document.MultiScores; }
		public Document Document;

		private Document.Settings _settings = new Document.Settings();
		public Document.Settings Settings { get => Document != null ? Document.CurrentSettings : _settings; }


		private readonly Dictionary<RegionType, RankedContig> SelectedContigs = new Dictionary<RegionType, RankedContig>();

		private readonly Dictionary<RegionType, ObjectListView> scoreViews = new Dictionary<RegionType, ObjectListView>();
		private readonly Dictionary<RegionType, ObjectListView> contigViews = new Dictionary<RegionType, ObjectListView>();
		private readonly Dictionary<RegionType, Chart[]> contigCharts = new Dictionary<RegionType, Chart[]>();
		private readonly Dictionary<RegionType, Chart> scoreCharts = new Dictionary<RegionType, Chart>();
		private readonly Dictionary<RegionType, SplitContainer> ScoreSplits = new Dictionary<RegionType, SplitContainer>();
		private readonly Dictionary<Control, Chart> ControlsWithDockedCharts = new Dictionary<Control, Chart>();
		private string EntryDbPath;
		public Dictionary<RegionType, RankedContig[]> SessionContigs = new Dictionary<RegionType, RankedContig[]>();
		public Dictionary<RegionType, List<RankedContig>> ExcludedContigs = new Dictionary<RegionType, List<RankedContig>>();

		//colors
		private readonly Color PeaksColor = Color.FromArgb(253, 174, 97);
		private readonly Color TemplateColor = Color.FromArgb(255, 115, 90);
		private readonly Color SpectrumColor = Color.FromArgb(171, 221, 164);
		private readonly Color ConservedColor = Color.FromArgb(43, 131, 186);
		private readonly Color MultiColor = Color.FromArgb(240, 163, 255);
		// ACDEFGHIKLMNPQRSTUVWYX
		// http://yulab-smu.top/ggmsa/articles/guides/Color_schemes_And_Font_Families.html#color-by-letter-1
		private readonly Color[] aaColorScheme = new Color[]
			{
				Color.FromArgb(240, 163, 255),    //A Amethyst
                Color.FromArgb(153, 63, 0),   //C Caramel
                Color.FromArgb(76, 0, 92),   //D Damson
                Color.FromArgb(255, 164, 5),   //E Orpiment
                Color.FromArgb(0, 92, 49),   //F Forest
                Color.FromArgb(43, 206, 72),   //G Green
                Color.FromArgb(255, 204, 153),   //H HoneyDew
                Color.FromArgb(128, 128, 128),   //I Iron
                Color.FromArgb(143, 124, 0),   //K Khaki
                Color.FromArgb(157, 204, 0),   //L Lime
                Color.FromArgb(194, 0, 136),   //M Mallow
                Color.FromArgb(0, 51, 128),   //N Navy
                Color.FromArgb(255, 168, 187),   //P Pink
                Color.FromArgb(66, 102, 0),   //Q Quagmire
                Color.FromArgb(224, 255, 102),   //R Uranium
                Color.FromArgb(94, 241, 242),   //S Sky
                Color.FromArgb(0, 153, 143),   //T Turquoise
                Color.FromArgb(255, 0, 16),   //U Read
                Color.FromArgb(116, 10, 255),   //V Violet
                Color.FromArgb(153, 0, 0),   //W Wine
                Color.FromArgb(255, 255, 128),   //Y Xanthin
                Color.FromArgb(25, 25, 25),   //X black
                Color.FromArgb(240, 163, 255),    //A Amethyst
                Color.FromArgb(153, 63, 0),   //C Caramel
                Color.FromArgb(76, 0, 92),   //D Damson
                Color.FromArgb(255, 164, 5),   //E Orpiment
                Color.FromArgb(0, 92, 49),   //F Forest
                Color.FromArgb(43, 206, 72),   //G Green
                Color.FromArgb(255, 204, 153),   //H HoneyDew
                Color.FromArgb(128, 128, 128),   //I Iron
                Color.FromArgb(143, 124, 0),   //K Khaki
                Color.FromArgb(157, 204, 0),   //L Lime
                Color.FromArgb(194, 0, 136),   //M Mallow
                Color.FromArgb(0, 51, 128),   //N Navy
                Color.FromArgb(255, 168, 187),   //P Pink
                Color.FromArgb(66, 102, 0),   //Q Quagmire
                Color.FromArgb(224, 255, 102),   //R Uranium
                Color.FromArgb(94, 241, 242),   //S Sky
                Color.FromArgb(0, 153, 143),   //T Turquoise
                Color.FromArgb(255, 0, 16),   //U Read
                Color.FromArgb(116, 10, 255),   //V Violet
                Color.FromArgb(153, 0, 0),   //W Wine
                Color.FromArgb(255, 255, 128),   //Y Xanthin
                Color.FromArgb(25, 25, 25),   //X black
            };
		#endregion

		#region Setup

		public FabLabForm(string path = null)
		{
			InitializeComponent();
			Initialize();
			if (path != null)
			{
				Document = Document.LoadFromInputClassPath(path, Settings);

				if (Document == null)
					throw new Exception();

				StartUp();
			}
		}

		private void Initialize()
		{
			propertyGrid.SelectedObject = Settings;
			propertyGrid.PropertyValueChanged += PropertyGrid_PropertyValueChanged;

			//tabcontrol
			mainTabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
			mainTabControl.DrawItem += MainTabControl_DrawItem;
			mainTabControl.MouseUp += MainTabControl_MouseUp;
			mainTabControl.SizeMode = TabSizeMode.Normal;
			mainTabControl.MouseDoubleClick += TabToWindow;

			templateObjectListView.FilterMenuBuildStrategy = new CustomFilterMenuBuilder();
			templateObjectListView.ShowItemToolTips = true;
			templateObjectListView.ShowFilterMenuOnRightClick = true;
			templateObjectListView.UseFiltering = true;
			templateObjectListView.ShowSortIndicators = true;
			templateObjectListView.FormatRow += TemplateObjectListView_FormatRow;

			ContextMenu cm = new ContextMenu();
			cm.MenuItems.Add("Spectrum", new EventHandler(ShowSpectrumView));
			cm.MenuItems.Add("ReadCoverage", new EventHandler(ShowReadCoverageView));
			templateObjectListView.ContextMenu = cm;

            foreach (var region in Helpers.GetAllRegionEnums())
			{
				// initializes if no values were found
				GetScoreSplit(region);
				GetScoreOlv(region);
				GetOlv(region);

				// initialize exclusion list
				ExcludedContigs.Add(region, new List<RankedContig>(10));
			}
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			const string message = "Are you sure that you would like to close fablab?";
			const string caption = "close fablab";
			var result = MessageBox.Show(message, caption,
										 MessageBoxButtons.YesNo,
										 MessageBoxIcon.Question);

			e.Cancel = (result == DialogResult.No);
		}

		#endregion

		#region Functions

		private void ShowReadCoverageView(object sender, EventArgs e)
		{
			var inspectTab = MakeClosableTab("Read coverage view", mainTabControl);
			var readDisplay = new SequenceCoverageDenovo();

			readDisplay.SetData("", "", Document.CurrentPrediction.Sequence, Document.NumberedReads.Select(x => (x.read.Peptide, x.numbering)).ToArray(), Document.CurrentPredictionNumbering);
			readDisplay.Parent = inspectTab;
			readDisplay.Dock = DockStyle.Fill;
		}

		private static TabPage MakeClosableTab(string title, TabControl tabControl)
		{
			var tab = new TabPage(title + "            ");
			tabControl.TabPages.Add(tab);
			return tab;
		}

		private void SetCurrentPrediction()
		{
			string seq = "";
			var nums = new List<double>();
			Peptide prediction = new Peptide("", "Prediction");


			foreach (var region in Helpers.GetAllRegionEnums())
			{
				if (!SelectedContigs.TryGetValue(region, out RankedContig middle))
				{
					GetConsensusContig(region, out double[] numbering, out string sequence);
					seq += sequence;
					nums.AddRange(numbering);
					continue;
				}

				if (region == RegionType.FR1)
				{
					prediction.Nterm = middle.contig.Nterm;
					seq += middle.contig.Sequence;
					nums.AddRange(middle.numbering);
					continue;
				}
				else if (region == RegionType.FR4)
				{
					prediction.Cterm = middle.contig.Cterm;
					seq += middle.contig.Sequence;
					nums.AddRange(middle.numbering);
					continue;
				}

				seq += middle.contig.Sequence;
				nums.AddRange(middle.numbering);
			}

			if (seq != Document.CurrentPrediction.Sequence)
			{
				prediction.Sequence = seq;
				prediction.Modifications = new Modification[prediction.Length];
				Document.CurrentPrediction = prediction;
				Document.CurrentPredictionNumbering = nums.ToArray();

				SetBestTemplate();
			}

			return;
		}

		private void ShowSpectrumView(object sender, EventArgs e)
		{
			var inspectTab = MakeClosableTab("Spectrum view", mainTabControl);

			var spectrumDisplay = new Ms2SpectrumGraph()
			{
				m_pSettings = new Ms2SpectrumGraph.Settings()
				{
					MinTopX = 10,
					MultiLine = true,
				},
			};

			spectrumDisplay.MouseUp += MouseUp;

			void MouseUp(object s, MouseEventArgs e2)
			{
				if (e2.Button == MouseButtons.Right)
				{
					((Control)s).ContextMenu.Show(this, e2.Location);
				}
			}

			void SaveToPdf(object s, EventArgs e2)
			{
				string filename = Path.GetTempFileName().Replace(".tmp", ".pdf");
				var doc = new iTextSharp.text.Document();
				Graphics parentGraphics = spectrumDisplay.CreateGraphics();
				float max = Math.Max(parentGraphics.VisibleClipBounds.Width, parentGraphics.VisibleClipBounds.Height);
				using (PDFMaker maker = new PDFMaker(filename, doc))
				{
					maker.InsertGDIPLUSDrawing(parentGraphics, spectrumDisplay.PrintPaint, new SizeF(parentGraphics.VisibleClipBounds.Width / max, parentGraphics.VisibleClipBounds.Height / max), oversizefactor: 2);
				}
				Process.Start(filename);
			}

			var cm = (MenuItem)sender;
			var olv = (ObjectListView)cm.GetContextMenu().SourceControl;

			// we construct the prediction out of all selected values
			SetCurrentPrediction();

			var asm = new AnnotatedSpectrumMatch(Document.Spectrum.WherePeakProperties(x => (x & 128) == 0), Document.CurrentPrediction, new PeptideFragment.FragmentModel(Document.AnnotationModel)
			{
				topx = 20,
				topx_massrange = 100,
			});

			ContextMenu icm = new ContextMenu();
			icm.MenuItems.Add("Export to Pdf", new EventHandler(SaveToPdf));
			icm.MenuItems.Add("Export to html", new EventHandler(SaveToHtml));
			spectrumDisplay.ContextMenu = icm;

			void SaveToHtml(object s, EventArgs e2)
			{
				string filename = Path.GetTempFileName().Replace(".tmp", ".html");
				var g = Graph.RenderSpectrum(asm);
				string html = Graph.CreateStandAloneFile("Test", g);

				File.WriteAllText(filename, html);
				Process.Start(filename);
			}

			spectrumDisplay.SetSpectrum(asm, new Ms2SpectrumGraph.Settings()
			{
				MinTopX = 20,
				MultiLine = true,
			});

			spectrumDisplay.Parent = inspectTab;
			spectrumDisplay.Dock = DockStyle.Fill;
		}

		private void ShowReadCoverageViewContig(object sender, EventArgs e)
		{
			try
			{
				var cm = (MenuItem)sender;
				var olv = (ObjectListView)cm.GetContextMenu().SourceControl;
				var item = (RankedContig)cm.GetContextMenu().Tag;

				var inspectTab = MakeClosableTab("Read coverage view", mainTabControl);

				RegionType curRegion = (RegionType)olv.Tag;

				double[] numbering;
				double floor = Helpers.GetBorders(curRegion).Item1;
				double ceiling = Helpers.GetBorders(curRegion).Item2;
				string sequence = "";
				if (!SelectedContigs.TryGetValue(curRegion, out RankedContig middle))
				{
					GetConsensusContig(curRegion, out numbering, out sequence);
				}
				else
				{
					floor = middle.numbering.Min();
					ceiling = middle.numbering.Max();
					numbering = middle.numbering;
					sequence = middle.contig.Sequence;
				}

				var readDisplay = new SequenceCoverageDenovo();


				ContextMenu icm = new ContextMenu();
				icm.MenuItems.Add("Export to Pdf", new EventHandler(SaveToPdf));
				readDisplay.ContextMenu = icm;

				void SaveToPdf(object s, EventArgs e2)
				{
					string filename = Path.GetTempFileName().Replace(".tmp", ".pdf");
					var doc = new iTextSharp.text.Document();
					Graphics parentGraphics = readDisplay.CreateGraphics();
					float max = Math.Max(parentGraphics.VisibleClipBounds.Width, parentGraphics.VisibleClipBounds.Height);
					using (PDFMaker maker = new PDFMaker(filename, doc))
					{
						maker.InsertGDIPLUSDrawing(parentGraphics, readDisplay.PrintPaint, new SizeF(parentGraphics.VisibleClipBounds.Width / max, parentGraphics.VisibleClipBounds.Height / max), oversizefactor: 2);
					}
					Process.Start(filename);
				}

				(string Peptide, double[] numbering)[] reads = Document.NumberedReads.Where(x =>
				{
					return x.numbering.Min().IsBetweenInclusive(floor, ceiling) || x.numbering.Max().IsBetweenInclusive(floor, ceiling) ||
						(x.numbering.Min() <= floor && x.numbering.Max() >= ceiling); // spans the region
				}).Select(x => (x.read.Peptide, x.numbering)).ToArray();

				readDisplay.SetData("", "", sequence, reads, numbering, drawletters: true);
				readDisplay.Parent = inspectTab;
				readDisplay.Dock = DockStyle.Fill;
			}
			catch (Exception er)
			{
				MessageBox.Show($"an error occured: {er.Message}");
			}
		}

		private void RemoveRead(object sender, EventArgs e)
		{
			try
			{
				var cm = (MenuItem)sender;
				var olv = (ObjectListView)cm.GetContextMenu().SourceControl;
				var item = (RankedContig)cm.GetContextMenu().Tag;

				RegionType curRegion = (RegionType)olv.Tag;

				var numbering = new List<double>();

				ExcludedContigs[curRegion].Add(item);

				FillContigandScoreViews(curRegion);
			}
			catch (Exception er)
			{
				MessageBox.Show($"an error occured: {er.Message}");
			}

		}

		private void ShowSpectrumViewContig(object sender, EventArgs e)
		{
			try
			{
				var cm = (MenuItem)sender;
				var olv = (ObjectListView)cm.GetContextMenu().SourceControl;
				var item = (RankedContig)cm.GetContextMenu().Tag;

				var inspectTab = MakeClosableTab("Spectrum view", mainTabControl);

				var spectrumDisplay = new Ms2SpectrumGraph()
				{
					m_pSettings = new Ms2SpectrumGraph.Settings()
					{
						MinTopX = 10,
						MultiLine = true,
					},
				};

				var asm = new AnnotatedSpectrumMatch(Document.Spectrum.WherePeakProperties(x => (x & 128) == 0), item.contig, new PeptideFragment.FragmentModel(Document.AnnotationModel)
				{
					topx = 20,
					topx_massrange = 100,
				});

				ContextMenu icm = new ContextMenu();
				icm.MenuItems.Add("Export to Pdf", new EventHandler(SaveToPdf));
				icm.MenuItems.Add("Export to html", new EventHandler(SaveToHtml));
				spectrumDisplay.ContextMenu = icm;

				void SaveToHtml(object s, EventArgs e2)
				{
					string filename = Path.GetTempFileName().Replace(".tmp", ".html");
					var g = Graph.RenderSpectrum(asm);
					string html = Graph.CreateStandAloneFile("Test", g);

					File.WriteAllText(filename, html);
					Process.Start(filename);
				}
				

				void SaveToPdf(object s, EventArgs e2)
				{
					string filename = Path.GetTempFileName().Replace(".tmp", ".pdf");
					var doc = new iTextSharp.text.Document();
					Graphics parentGraphics = spectrumDisplay.CreateGraphics();
					float max = Math.Max(parentGraphics.VisibleClipBounds.Width, parentGraphics.VisibleClipBounds.Height);
					using (PDFMaker maker = new PDFMaker(filename, doc))
					{
						maker.InsertGDIPLUSDrawing(parentGraphics, spectrumDisplay.PrintPaint, new SizeF(parentGraphics.VisibleClipBounds.Width / max, parentGraphics.VisibleClipBounds.Height / max), oversizefactor: 2);
					}
					Process.Start(filename);
				}

				var numbering = new List<double>();

				Peptide prediction = item.contig;
				if ((prediction.Modifications ?? new Modification[0]).Length != prediction.Sequence.Length)
				{
					prediction.Modifications = new Modification[prediction.Length];
				}

				spectrumDisplay.SetSpectrum(asm, new Ms2SpectrumGraph.Settings()
				{
					MinTopX = 10,
					MultiLine = true,
				});

				spectrumDisplay.Parent = inspectTab;
				spectrumDisplay.Dock = DockStyle.Fill;
			}
			catch (Exception er)
			{
				MessageBox.Show($"an error occured: {er.Message}");
			}
		}

		private void ShowMultiScoreForAll(object sender, EventArgs e)
		{
			try
			{
				var cm = (MenuItem)sender;
				var olv = (ObjectListView)cm.GetContextMenu().SourceControl;
				var item = (RankedContig)cm.GetContextMenu().Tag;

				RankedContig[] rankedContigs = olv.FilteredObjects.Cast<RankedContig>().ToArray();
				var range = OLVDoublePrompt.ShowDialog(0, rankedContigs.Select(x => x.contig.Length).Max()).Select(x => (int)x).ToArray();
				var ranked = Helpers.RankFullLength(rankedContigs.Select(x => (x.contig, x.numbering)).ToArray(), SequenceSource.Contig, Document, Document.Locus, range);
				ShowPredictionsView(ranked, range);
			}
			catch (Exception er)
			{
				MessageBox.Show($"an error occured: {er.Message}");
			}
		}

		private void RefineAll(object sender, EventArgs e)
		{
			try
			{
				var cm = (MenuItem)sender;
				var olv = (ObjectListView)cm.GetContextMenu().SourceControl;
				var item = (RankedContig)cm.GetContextMenu().Tag;

				RankedContig[] rankedContigs = olv.FilteredObjects.Cast<RankedContig>().ToArray();
				RankedContig[] rankedContigsCopy = new RankedContig[rankedContigs.Length];
				rankedContigs.CopyTo(rankedContigsCopy, 0);

				var range = OLVDoublePrompt.ShowDialog(0, rankedContigs.Select(x => x.contig.Length).Max()).Select(x => (int)x).ToArray();

				ShowRefinedPredictionsView(rankedContigsCopy, range);
			}
			catch (Exception er)
			{
				MessageBox.Show($"an error occured: {er.Message}");
			}
		}

		private void Reload()
		{
			this.Text = Document.Title;

			PopulateContigListViews();

			SetCurrentPrediction();

			PopulateTemplateListView();
		}

		private void PopulateContigListViews()
		{
			// fill FR
			foreach (var region in Helpers.GetAllRegionEnums())
			{
				if (Helpers.IsCdr(region))
					continue;

				FillContigandScoreViews(region);
			}

			// cdr
			foreach (var region in Helpers.GetAllRegionEnums())
			{
				if (!Helpers.IsCdr(region))
					continue;
				FillContigandScoreViews(region);
			}
		}

		private void InitializeFrContigAndScoreViews()
		{
			foreach (var region in Helpers.GetAllRegionEnums())
			{
				// parse the name
				RegionType curRegion = (RegionType)region;
				if (curRegion == RegionType.None)
					continue;
				if (Helpers.IsCdr(curRegion))
					continue;

				InitializeContigListView(curRegion);

				InitializeScoreListViews(curRegion);
			}
		}

		private void InitializeCdrContigAndScoreViews()
		{
			foreach (var region in Helpers.GetAllRegionEnums())
			{
				// parse the name
				RegionType curRegion = (RegionType)region;
				if (curRegion == RegionType.None)
					continue;
				if (!Helpers.IsCdr(curRegion))
					continue;

				FillIfCdr(curRegion);

				InitializeContigListView(curRegion);

				InitializeScoreListViews(curRegion);
			}
		}

		private void InitializeScoreListViews(RegionType curRegion)
		{
			// get the objects
			RankedContig[] contigs = GetSessionClippedContigs(curRegion);

			var varsToOrderOn = Document.CurrentSettings.OrderingVars[curRegion];
			if (varsToOrderOn.Contains(5) && !Document.CurrentSettings.UseMultiScore)
			{
				var l = varsToOrderOn.ToList();
				l.Add(1);
				varsToOrderOn = l.ToArray();
			}
			contigs = Document.Reorder(contigs, varsToOrderOn);

			// create the olv and columns
			ObjectListView scoreOlv = GetScoreOlv(curRegion);
			AddColumns2(contigs, scoreOlv, curRegion);

			scoreOlv.RebuildColumns();
		}

		private void AddColumns(RankedContig[] contigs, ObjectListView olv, bool addNterm = true)
		{
			IEnumerable<object> objectsToBe = contigs.Select(x => (object)x);
			var numInRange = Helpers.OrderImgtNumbering(contigs.Where(x => x.numbering != null).SelectMany(x => x.numbering).ToArray());
			if (addNterm)
			{

				Helpers.CreateColumn(olv, "#", HorizontalAlignment.Right, delegate (Object obj)
				{
					return "";
				}, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);

				Helpers.CreateColumn(olv, "Nterm", HorizontalAlignment.Right, delegate (Object obj)
				{
					var parsed = (RankedContig)obj;
					if (parsed.contig.Nterm == null)
						return "";
					else
						return Math.Round(parsed.contig.Nterm.Delta, 3);
				}, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
				olv.AllColumns.Last().IsVisible = true;
				olv.AllColumns.Last().Sortable = true;

				Helpers.CreateColumn(olv, "Sequence", HorizontalAlignment.Left, delegate (Object obj)
				{
					var parsed = (RankedContig)obj;
					var numberedContig = (parsed.contig.Sequence, parsed.numbering);
					char[] charArray = ConvertToGappedString(numberedContig, numInRange);
					string sequence = new string(charArray);

					if (sequence == null)
						return "Unknown";
					else
						return sequence;
				}, customFilterMenuBuilder: new StringFilterMenuBuilder(), objectsToBe: objectsToBe);
				olv.AllColumns.Last().FillsFreeSpace = true;
				olv.AllColumns.Last().Sortable = true;

				olv.AllColumns.Last().TextAlign = HorizontalAlignment.Right;
				Helpers.CreateColumn(olv, "Cterm", HorizontalAlignment.Left, delegate (Object obj)
				{
					var parsed = (RankedContig)obj;
					if (parsed.contig.Cterm == null)
						return "";
					else
						return Math.Round(parsed.contig.Cterm.Delta, 3);
				}, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
				olv.AllColumns.Last().Sortable = true;
			}

			Helpers.CreateColumn(olv, "SumRank", HorizontalAlignment.Left, delegate (Object obj)
			{
				return ((RankedContig)obj).sumR;
			}, sortable: true, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			olv.AllColumns.Last().IsVisible = addNterm;

			// we set the width to either the width of the manestring, or the width of the widest value.
			Helpers.CreateColumn(olv, "PEAKS", HorizontalAlignment.Left, delegate (Object obj)
			{
				return ((RankedContig)obj).peaks;
			}, sortable: true, objectsToBe: objectsToBe);
			olv.AllColumns.Last().IsVisible = addNterm;

			var hfs = new HeaderFormatStyle();
			hfs.SetBackColor(PeaksColor);
			olv.AllColumns.Last().HeaderFormatStyle = hfs;
			Helpers.CreateColumn(olv, "# PEAKS", HorizontalAlignment.Left, delegate (Object obj)
			{
				return ((RankedContig)obj).peaksR;
			}, sortable: true, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			olv.AllColumns.Last().IsVisible = addNterm;
			hfs = new HeaderFormatStyle();
			hfs.SetBackColor(PeaksColor);
			olv.AllColumns.Last().HeaderFormatStyle = hfs;
			Helpers.CreateColumn(olv, "Template", HorizontalAlignment.Left, delegate (Object obj)
			{
				return ((RankedContig)obj).template;
			}, sortable: true, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			olv.AllColumns.Last().IsVisible = addNterm;
			hfs = new HeaderFormatStyle();
			hfs.SetBackColor(TemplateColor);
			olv.AllColumns.Last().HeaderFormatStyle = hfs;
			olv.AllColumns.Last().IsVisible = false;

			Helpers.CreateColumn(olv, "# Template", HorizontalAlignment.Left, delegate (Object obj)
			{
				return ((RankedContig)obj).templateR;
			}, sortable: true, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			olv.AllColumns.Last().IsVisible = addNterm;
			hfs = new HeaderFormatStyle();
			hfs.SetBackColor(TemplateColor);
			olv.AllColumns.Last().HeaderFormatStyle = hfs;
			olv.AllColumns.Last().IsVisible = false;
			Helpers.CreateColumn(olv, "Spectrum", HorizontalAlignment.Left, delegate (Object obj)
			{
				return Math.Round((double)((RankedContig)obj).spectrum, 3);
			}, sortable: true, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			olv.AllColumns.Last().IsVisible = addNterm;
			hfs = new HeaderFormatStyle();
			hfs.SetBackColor(SpectrumColor);
			olv.AllColumns.Last().HeaderFormatStyle = hfs;
			Helpers.CreateColumn(olv, "# Spectrum", HorizontalAlignment.Left, delegate (Object obj)
			{
				return ((RankedContig)obj).spectrumR;
			}, sortable: true, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			olv.AllColumns.Last().IsVisible = addNterm;
			hfs = new HeaderFormatStyle();
			hfs.SetBackColor(SpectrumColor);
			olv.AllColumns.Last().HeaderFormatStyle = hfs;
			Helpers.CreateColumn(olv, "K-mer", HorizontalAlignment.Left, delegate (Object obj)
			{
				return ((RankedContig)obj).multi;
			}, sortable: true, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			olv.AllColumns.Last().IsVisible = addNterm;
			hfs = new HeaderFormatStyle();
			hfs.SetBackColor(MultiColor);
			olv.AllColumns.Last().HeaderFormatStyle = hfs;
			Helpers.CreateColumn(olv, "# K-mer", HorizontalAlignment.Left, delegate (Object obj)
			{
				return ((RankedContig)obj).multiR;
			}, sortable: true, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			olv.AllColumns.Last().IsVisible = addNterm;
			hfs = new HeaderFormatStyle();
			hfs.SetBackColor(MultiColor);
			olv.AllColumns.Last().HeaderFormatStyle = hfs;
			Helpers.CreateColumn(olv, "Germline", HorizontalAlignment.Left, delegate (Object obj)
			{
				return Math.Round((double)((RankedContig)obj).conservedness, 3);
			}, sortable: true, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			olv.AllColumns.Last().IsVisible = addNterm;
			hfs = new HeaderFormatStyle();
			hfs.SetBackColor(ConservedColor);
			olv.AllColumns.Last().IsVisible = false;
			olv.AllColumns.Last().HeaderFormatStyle = hfs;
			Helpers.CreateColumn(olv, "# Germline", HorizontalAlignment.Left, delegate (Object obj)
			{
				return ((RankedContig)obj).conservednessR;
			}, sortable: true, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			hfs = new HeaderFormatStyle();
			olv.AllColumns.Last().IsVisible = addNterm;
			hfs.SetBackColor(ConservedColor);
			olv.AllColumns.Last().IsVisible = false;
			olv.AllColumns.Last().HeaderFormatStyle = hfs;


			olv.FormatRow += delegate (object sender, FormatRowEventArgs args)
			{
				args.Item.Text = args.RowIndex.ToString();
			};
		}

		private void AddColumns2(RankedContig[] contigs, ObjectListView olv, RegionType curRegion)
		{
			IEnumerable<object> objectsToBe = contigs.Select(x => (object)x);
			var numInRange = Helpers.OrderImgtNumbering(contigs.Where(x => x.numbering != null).SelectMany(x => x.numbering).ToArray());

			Helpers.CreateColumn(olv, "#", HorizontalAlignment.Right, delegate (Object obj)
			{
				return "";
			}, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);

			Helpers.CreateColumn(olv, "Nterm", HorizontalAlignment.Right, delegate (Object obj)
			{
				var parsed = (RankedContig)obj;
				if (parsed.contig.Nterm == null)
					return "";
				else
					return Math.Round(parsed.contig.Nterm.Delta, 3);
			}, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			olv.AllColumns.Last().IsVisible = true;
			olv.AllColumns.Last().Sortable = true;

			Helpers.CreateColumn(olv, "Sequence", HorizontalAlignment.Left, delegate (Object obj)
			{
				var parsed = (RankedContig)obj;
				var numberedContig = (parsed.contig.Sequence, parsed.numbering);
				char[] charArray = ConvertToGappedString(numberedContig, curRegion);
				string sequence = new string(charArray);

				if (sequence == null)
					return "Unknown";
				else
					return sequence;
			}, customFilterMenuBuilder: new StringFilterMenuBuilder(), objectsToBe: objectsToBe);
			olv.AllColumns.Last().FillsFreeSpace = true;
			olv.AllColumns.Last().Sortable = true;

			olv.AllColumns.Last().TextAlign = HorizontalAlignment.Right;
			Helpers.CreateColumn(olv, "Cterm", HorizontalAlignment.Left, delegate (Object obj)
			{
				var parsed = (RankedContig)obj;
				if (parsed.contig.Cterm == null)
					return "";
				else
					return Math.Round(parsed.contig.Cterm.Delta, 3);
			}, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			olv.AllColumns.Last().Sortable = true;

			Helpers.CreateColumn(olv, "SumRank", HorizontalAlignment.Left, delegate (Object obj)
			{
				return ((RankedContig)obj).sumR;
			}, sortable: true, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);

			// we set the width to either the width of the manestring, or the width of the widest value.
			Helpers.CreateColumn(olv, "PEAKS", HorizontalAlignment.Left, delegate (Object obj)
			{
				return ((RankedContig)obj).peaks;
			}, sortable: true, objectsToBe: objectsToBe);
			var hfs = new HeaderFormatStyle();
			hfs.SetBackColor(PeaksColor);
			olv.AllColumns.Last().HeaderFormatStyle = hfs;
			Helpers.CreateColumn(olv, "# PEAKS", HorizontalAlignment.Left, delegate (Object obj)
			{
				return ((RankedContig)obj).peaksR;
			}, sortable: true, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			hfs = new HeaderFormatStyle();
			hfs.SetBackColor(PeaksColor);
			olv.AllColumns.Last().HeaderFormatStyle = hfs;
			Helpers.CreateColumn(olv, "Template", HorizontalAlignment.Left, delegate (Object obj)
			{
				return ((RankedContig)obj).template;
			}, sortable: true, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			hfs = new HeaderFormatStyle();
			hfs.SetBackColor(TemplateColor);
			olv.AllColumns.Last().HeaderFormatStyle = hfs;
			olv.AllColumns.Last().IsVisible = false;
			Helpers.CreateColumn(olv, "# Template", HorizontalAlignment.Left, delegate (Object obj)
			{
				return ((RankedContig)obj).templateR;
			}, sortable: true, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			hfs = new HeaderFormatStyle();
			hfs.SetBackColor(TemplateColor);
			olv.AllColumns.Last().HeaderFormatStyle = hfs;
			olv.AllColumns.Last().IsVisible = false;
			Helpers.CreateColumn(olv, "Spectrum", HorizontalAlignment.Left, delegate (Object obj)
			{
				return Math.Round((double)((RankedContig)obj).spectrum, 3);
			}, sortable: true, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			hfs = new HeaderFormatStyle();
			hfs.SetBackColor(SpectrumColor);
			olv.AllColumns.Last().HeaderFormatStyle = hfs;
			Helpers.CreateColumn(olv, "# Spectrum", HorizontalAlignment.Left, delegate (Object obj)
			{
				return ((RankedContig)obj).spectrumR;
			}, sortable: true, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			hfs = new HeaderFormatStyle();
			hfs.SetBackColor(SpectrumColor);
			olv.AllColumns.Last().HeaderFormatStyle = hfs;
			Helpers.CreateColumn(olv, "K-mer", HorizontalAlignment.Left, delegate (Object obj)
			{
				return ((RankedContig)obj).multi;
			}, sortable: true, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			hfs = new HeaderFormatStyle();
			hfs.SetBackColor(MultiColor);
			olv.AllColumns.Last().HeaderFormatStyle = hfs;
			Helpers.CreateColumn(olv, "# K-mer", HorizontalAlignment.Left, delegate (Object obj)
			{
				return ((RankedContig)obj).multiR;
			}, sortable: true, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			hfs = new HeaderFormatStyle();
			hfs.SetBackColor(MultiColor);
			olv.AllColumns.Last().HeaderFormatStyle = hfs;
			Helpers.CreateColumn(olv, "Germline", HorizontalAlignment.Left, delegate (Object obj)
			{
				return Math.Round((double)((RankedContig)obj).conservedness, 3);
			}, sortable: true, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			hfs = new HeaderFormatStyle();
			hfs.SetBackColor(ConservedColor);
			olv.AllColumns.Last().IsVisible = false;
			olv.AllColumns.Last().HeaderFormatStyle = hfs;
			Helpers.CreateColumn(olv, "# Germline", HorizontalAlignment.Left, delegate (Object obj)
			{
				return ((RankedContig)obj).conservednessR;
			}, sortable: true, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			hfs = new HeaderFormatStyle();
			hfs.SetBackColor(ConservedColor);
			olv.AllColumns.Last().IsVisible = false;
			olv.AllColumns.Last().HeaderFormatStyle = hfs;


			olv.FormatRow += delegate (object sender, FormatRowEventArgs args)
			{
				args.Item.Text = args.RowIndex.ToString();
			};
		}

		private void InitializeContigListView(RegionType curRegion)
		{
			string regionName = Enum.GetName(typeof(RegionType), curRegion);

			// get the matching controls
			ObjectListView olv = GetOlv(curRegion);
			// get the objects
			RankedContig[] contigs = GetSessionClippedContigs(curRegion);
			var varsToOrderOn = Document.CurrentSettings.OrderingVars[curRegion];
			if (varsToOrderOn.Contains(5) && !Document.CurrentSettings.UseMultiScore)
			{
				var l = varsToOrderOn.ToList();
				l.Add(1);
				varsToOrderOn = l.ToArray();
			}
			contigs = Document.Reorder(contigs, varsToOrderOn);

			IEnumerable<object> objectsToBe = contigs.Select(x => (object)x);
			var numInRange = Helpers.OrderImgtNumbering(contigs.SelectMany(x => x.numbering).ToArray());

			// checkbox column
			Helpers.CreateColumn(olv, "", HorizontalAlignment.Right, delegate (Object obj)
			{
				return "";
			}, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);

			Helpers.CreateColumn(olv, "NTerm", HorizontalAlignment.Left, delegate (Object obj)
			{
				var parsed = (RankedContig)obj;

				RegionType nFlank = RegionType.CDR3;
				if (curRegion != RegionType.FR4)
					nFlank = Helpers.GetFlanking(curRegion).Item1;

				if (!SelectedContigs.TryGetValue(nFlank, out RankedContig nContig))
					return Math.Round(parsed.contig.Nterm.Delta, 1);

				Peptide cpep = parsed.contig;
				Peptide nPep = nContig.contig;
				return GetNGap(nPep, cpep);
			}, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			olv.AllColumns.Last().Sortable = true;
			olv.AllColumns.Last().IsVisible = false;

			Helpers.CreateColumn(olv, regionName, HorizontalAlignment.Left, delegate (Object obj)
			{
				var parsed = (RankedContig)obj;
				var numberedContig = (parsed.contig.Sequence, parsed.numbering);
				char[] charArray = ConvertToGappedString(numberedContig, curRegion);
				string sequence = new string(charArray);

				if (sequence == null)
					return "Unknown";
				else
					return sequence;
			}, customFilterMenuBuilder: new StringFilterMenuBuilder(), objectsToBe: objectsToBe);

			olv.AllColumns.Last().FillsFreeSpace = true;

			Helpers.CreateColumn(olv, "CTerm", HorizontalAlignment.Left, delegate (Object obj)
			{
				var parsed = (RankedContig)obj;

				RegionType cFlank = RegionType.CDR1;
				if (curRegion != RegionType.FR1)
					cFlank = Helpers.GetFlanking(curRegion).Item2;
				if (!SelectedContigs.TryGetValue(cFlank, out RankedContig cContig))
					return Math.Round(parsed.contig.Cterm.Delta, 1);

				Peptide npep = parsed.contig;
				Peptide cPep = cContig.contig;

				return GetCGap(npep, cPep);
			}, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			olv.AllColumns.Last().Sortable = true;

			AddColumns(contigs, olv, false);

			Helpers.CreateColumn(olv, "Source", HorizontalAlignment.Left, delegate (Object obj)
			{
				var parsed = (RankedContig)obj;

				return Enum.GetName(typeof(SequenceSource), parsed.origin);
			}, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);

			olv.AllColumns.Last().IsVisible = false;

			// we want the last column to fill up the olv
			olv.RebuildColumns();
		}

		private void FillContigandScoreViews(RegionType curRegion)
		{
			// get the matching controls
			ObjectListView olv = GetOlv(curRegion);
			ObjectListView scoreOlv = GetScoreOlv(curRegion);

			//only executes if the region is a cdr
			FillIfCdr(curRegion);

			// get the objects
			RankedContig[] contigs = GetSessionClippedContigs(curRegion);

			var varsToOrderOn = Document.CurrentSettings.OrderingVars[curRegion];
			if (varsToOrderOn.Contains(5) && !Document.CurrentSettings.UseMultiScore)
			{
				var l = varsToOrderOn.ToList();
				l.Add(1);
				varsToOrderOn = l.ToArray();
			}
			contigs = Document.Reorder(contigs, varsToOrderOn);

			// nothing changed
			if (olv.Objects != null && olv.Objects.Cast<RankedContig>().SequenceEqual(contigs))
				return;


			olv.SetObjects(contigs);
			scoreOlv.SetObjects(contigs);

			// update the gapped string dictionary
			foreach (var contig in contigs)
			{
				ConvertToGappedString((contig.contig.Sequence, contig.numbering), curRegion, overrideIt: true);
			}

			var chart = scoreCharts[curRegion];

			chart.ChartAreas.Clear();
			chart.Series.Clear();
			chart.Legends.Clear();

			try
			{
				DrawDotPlotForContigs(contigs, chart);
				//Draw2DPlot(contigs, chart);
			}
			catch (Exception err)
			{
				chart.ChartAreas.Clear();
				chart.Series.Clear();
				MessageBox.Show($@"Error building chart {curRegion}: {err.Message}");
			}

			if (olv.CheckedIndices.Count == 0 && olv.Items.Count != 0)
			{
				olv.CheckObject(olv.GetModelObject(0));
				SetCurrentSelection(curRegion, (RankedContig)olv.GetModelObject(0));
			}
		}

		/// <summary>
		/// if nothing in the sessiondict, we get it from the docDict
		/// </summary>
		/// <param name="curRegion"></param>
		/// <returns></returns>
		private RankedContig[] GetSessionClippedContigs(RegionType curRegion)
		{
			if (!SessionContigs.TryGetValue(curRegion, out RankedContig[] res))
			{
				if (!Document.ClippedContigs.TryGetValue(curRegion, out res))
				{
					return new RankedContig[0];
				}
			}

			if (Helpers.IsCdr(curRegion))
			{
				res = res.Where(x => !ExcludedContigs[curRegion].Select(y => y.contig.Sequence).Contains(x.contig.Sequence)).ToArray();
			}
			else
			{
				res = res.Where(x => !ExcludedContigs[curRegion].Contains(x)).ToArray();
			}

			return FilterContigsToSettings(curRegion, res);
		}

		/// <summary>
		/// Automatically adds from the document, which are filtered out in the get method
		/// </summary>
		/// <param name="curRegion"></param>
		/// <param name="ranked"></param>
		private void SetSessionClippedContigs(RegionType curRegion, RankedContig[] ranked)
		{
			var innerRanked = ranked.ToList();
			if (Document.ClippedContigs.TryGetValue(curRegion, out RankedContig[] res))
			{
				// add from the document;
				innerRanked.AddRange(res);
			}

			innerRanked = innerRanked.Where(x => !ExcludedContigs[curRegion].Contains(x)).ToList();

			// check if the dictionary is already filled
			if (!SessionContigs.TryGetValue(curRegion, out RankedContig[] res2))
			{
				SessionContigs.Add(curRegion, innerRanked.ToArray());
			}
			else
			{
				SessionContigs[curRegion] = innerRanked.ToArray();
			}
		}

		/// <summary>
		/// Automatically adds from the document, which are filtered out in the get method. This needs to be done after the clipandrank method
		/// </summary>
		/// <param name="curRegion"></param>
		/// <param name="ranked"></param>
		private void UpdateSessionClippedContigs(RegionType curRegion)
		{
			var innerRanked = new List<RankedContig>();
			if (Document.ClippedContigs.TryGetValue(curRegion, out RankedContig[] res))
			{
				// add from the document;
				innerRanked.AddRange(res);
			}

			// check if then dictionary is already filled
			if (!SessionContigs.TryGetValue(curRegion, out RankedContig[] res2))
			{
				SessionContigs.Add(curRegion, innerRanked.ToArray());
			}
			else
			{
				SessionContigs[curRegion] = innerRanked.ToArray();
			}
		}

		private RankedContig[] FilterContigsToSettings(RegionType curRegion, RankedContig[] contigs)
		{
			var innerContigs = contigs.Select(x => x);
			// filter the regions to be displayed
			if (curRegion == RegionType.FR1)
			{
				innerContigs = innerContigs.Where(x => Math.Abs(x.contig.Nterm.Delta) < Settings.MaxNDelta).ToList();
			}
			else if (curRegion == RegionType.FR4)
			{
				innerContigs = innerContigs.Where(x => Math.Abs(x.contig.Cterm.Delta) < Settings.MaxCDelta).ToList();
			}
			if (!Helpers.IsCdr(curRegion))
			{

				innerContigs = innerContigs.Where(x => x.contig.Length > 2);
				var (startImgt, endImgt) = Helpers.GetBorders(curRegion);

				if (startImgt == double.MinValue)
					startImgt = 1;

				innerContigs = innerContigs.Where(x => x.numbering.Min().IsBetweenInclusive(startImgt - 0.01, startImgt + Settings.NumberingTolerance + 0.01)).ToList();

				if (endImgt == double.MaxValue)
					endImgt = Document.NumberedTemplates.First().numbering.Max();
				innerContigs = innerContigs.Where(x => x.numbering.Max().IsBetweenInclusive(endImgt - Settings.NumberingTolerance - 0.01, endImgt + 0.01)).ToList();
			}
			else
			{
				// we also want to only display the selected kind of fillers
				var sourcesToDisplay = Settings.SequenceSourceToDisplay.Where(x => x.Value).Select(x => x.Key);
				innerContigs = innerContigs.Where(x => sourcesToDisplay.Contains(x.origin)).ToArray();
			}
			contigs = innerContigs.ToArray();
			return contigs;
		}

		private static double GetCGap(Peptide npep, Peptide cPep)
		{
			var cOffsetNPep = npep.Cterm.Delta;
			var flank = cPep.Sequence.Select(aa => AminoAcid.Get(aa).MonoIsotopicWeight).Sum() + (cPep.Cterm == null ? 0 : cPep.Cterm.Delta);

			double gap = cOffsetNPep - flank;
			return Math.Round(gap, 3);
		}

		private static double GetNGap(Peptide nPep, Peptide cPep)
		{
			var nOffsetCPep = cPep.Nterm.Delta;
			var n = nPep.Sequence.Select(aa => AminoAcid.Get(aa).MonoIsotopicWeight).Sum() + (nPep.Nterm == null ? 0 : nPep.Nterm.Delta);

			double gap = nOffsetCPep - n;
			return Math.Round(gap, 3);
		}

		/// <summary>
		/// either from gapfilling or from the fillerdict. clipped contigs not included
		/// </summary>
		/// <param name="nFlank"></param>
		/// <param name="cFlank"></param>
		/// <param name="region"></param>
		/// <returns></returns>
		public RankedContig[] FillersFromFlanks(RankedContig nFlank, RankedContig cFlank, RegionType region)
		{
			var numberedTemplates = Document.NumberedTemplates.Select(x => (new Peptide(x.read), x.numbering)).ToList();

			var tags = new List<Peptide>
						{
							nFlank.contig,
							cFlank.contig,
						};
			(double nFlankOffset, string nFlankEnd, double cFlankOffset, string cFlankEnd) key = KeyFromFlanks(nFlank, cFlank);
			RankedContig[] ranked = new RankedContig[0];

			if (!Document.FillerDict.TryGetValue(key, out ranked))
			{
				Tolerance innerTol = new Tolerance(Settings.ToleranceInDaGapFillers, Tolerance.ErrorUnit.THOMPSON);
				var numbered = GetGapFillingReads().ToList();
				var consensusPeptide = GetConsensusRankedContig();

				var rankedList = Helpers.Rank(numbered.Where(x =>
				{
					var gapsize = GetNGap(nFlank.contig, cFlank.contig);
					return innerTol.IsDuplicate(gapsize, x.x.Sequence.Select(aa => AminoAcid.Get(aa).MonoIsotopicWeight).Sum());
				}).ToArray(), Document, SequenceSource.Reads, region).ToList();

				if (Settings.AddReadsBasedOnNumber)
				{
					//rankedList.Add(consensusPeptide);
					var numbered2 = GetGapFillingReads2();

					rankedList.AddRange(Helpers.Rank(numbered2.Where(x =>
					{
						var gapsize = GetNGap(nFlank.contig, cFlank.contig);
						return innerTol.IsDuplicate(gapsize, x.peptide.Sequence.Select(aa => AminoAcid.Get(aa).MonoIsotopicWeight).Sum());
					}).ToArray(), Document, SequenceSource.TwoReads, region).ToList());
				}

				ranked = rankedList.GroupBy(x => x.contig.Sequence).Select(x => x.First()).ToArray();
				Document.FillerDict.Add(key, ranked);
			}

			return ranked;

			RankedContig GetConsensusRankedContig()
			{
				var cons = GetConsensusPeptide(region, nFlank.contig);
				double[] consNumber = ImgtNumberer.NumberCdr(region, cons.Sequence);
				var consArray = new (Peptide, double[])[] { (cons, consNumber) };
				var rankedListCons = Helpers.Rank(consArray, Document, SequenceSource.Consensus, region).ToList();
				return rankedListCons.First();
			}

			(Peptide x, double[])[] GetGapFillingReads()
			{
				var refSeq = ReadFilter.GetAlternativeConsensusSequence(ReadFilter.GetConsensusFromReadsOriginal(Document.NumberedReads), (new Peptide(Document.NumberedTemplates.First().read), Document.NumberedTemplates.First().numbering)).sequence;

				(((Peptide tag, double nDiff) first, (Peptide tag, double nDiff) second, double) tagsWithGap, List<(PeaksPeptideData pep, Peptide, double)> gapFillingPeptides, List<(PeaksPeptideData pep, Peptide, double)> leftExtensions, List<(PeaksPeptideData pep, Peptide, double)> rightExtensions)[] gapfillers = Helpers.SelectCandidateFillers(Document.NumberedReads.Select(x => x.read), tags, new Peptide(refSeq), Document.Spectrum.Precursor);


				Tolerance innerTol = new Tolerance(Settings.ToleranceInDaGapFillers, Tolerance.ErrorUnit.THOMPSON);
				List<Peptide> seeds = Helpers.FillGapsWithCandidates(gapfillers, innerTol, Document.Spectrum).GroupBy(x => x.GetModifiedSequence()).Select(x => x.First()).ToList();

				// gotta number the reads.
				var numbered = seeds.Select(x => (x, ImgtNumberer.NumberCdr(region, x.Sequence))).ToArray();

				return numbered;
			}

			List<(Peptide peptide, double[] numbering)> GetGapFillingReads2()
			{
				var precursorMass = Proteomics.ConvertAverageMassToMonoIsotopic(Document.Spectrum.PrecursorMass, true) - MassSpectrometry.MassWater;

				double floor = Helpers.GetBorders(region).Item1;
				double ceiling = Helpers.GetBorders(region).Item2;

				var n = Modification.CreateOffsetMod(0, nFlank.contig.Nterm.Delta + nFlank.contig.Sequence.Select(aa => AminoAcid.Get(aa).MonoIsotopicWeight).Sum(), Modification.PositionType.anyNterm, Modification.TerminusType.nterm);


				Tolerance innerTol = new Tolerance(Settings.ToleranceInDaGapFillers, Tolerance.ErrorUnit.THOMPSON);


				var c = Modification.CreateOffsetMod(0, precursorMass - (nFlank.contig.Nterm.Delta), Modification.PositionType.anyCterm, Modification.TerminusType.cterm);
				var reads = Document.NumberedReads.Where(x =>
				{
					// ends in the region
					return x.numbering.Min().IsBetweenInclusive(floor, ceiling) || x.numbering.Max().IsBetweenInclusive(floor, ceiling) ||
					(x.numbering.Min() <= floor && x.numbering.Max() >= ceiling); // spans the region
				}).GroupBy(x => x.read.Peptide).Select(x => x.First()).Select(x =>
				{
					var pep = new Peptide(x.read.Peptide, "")
					{
						Nterm = n,
						Cterm = c
					};
					return (pep, x.numbering);
				}).ToList();

				List<(Peptide clipped, double[] numbering)> ans = Document.ClipToRegion(reads, region, false).ToList();
				return ans.Select(x =>
				{
					var pep = x.clipped;
					var c2 = Modification.CreateOffsetMod(0, precursorMass - (nFlank.contig.Nterm.Delta + nFlank.contig.Sequence.Select(aa => AminoAcid.Get(aa).MonoIsotopicWeight).Sum() + pep.Sequence.Select(aa => AminoAcid.Get(aa).MonoIsotopicWeight).Sum()), Modification.PositionType.anyCterm, Modification.TerminusType.cterm);

					pep.Nterm = n;
					pep.Cterm = c2;
					return (pep, x.numbering);
				}).ToList();
			}
		}

		private Peptide GetConsensusPeptide(RegionType region, Peptide nFlank)
		{
			var precursorMass = Proteomics.ConvertAverageMassToMonoIsotopic(Document.Spectrum.PrecursorMass, true) - MassSpectrometry.MassWater;

			GetConsensusContig(region, out double[] numbering, out string sequence);

			var n = Modification.CreateOffsetMod(0, nFlank.Nterm.Delta + nFlank.Sequence.Select(aa => AminoAcid.Get(aa).MonoIsotopicWeight).Sum(), Modification.PositionType.anyNterm, Modification.TerminusType.nterm);
			var c = Modification.CreateOffsetMod(0, precursorMass - (nFlank.Nterm.Delta + nFlank.Sequence.Select(aa => AminoAcid.Get(aa).MonoIsotopicWeight).Sum() + sequence.Select(aa => AminoAcid.Get(aa).MonoIsotopicWeight).Sum()), Modification.PositionType.anyCterm, Modification.TerminusType.cterm);
			var consensusPeptide = new Peptide(sequence)
			{
				Nterm = n,
				Cterm = c,
			};
			return consensusPeptide;
		}

		private static (double nFlankOffset, string nFlankEnd, double cFlankOffset, string cFlankEnd) KeyFromFlanks(RankedContig nFlank, RankedContig cFlank)
		{
			double nFlankOffset = nFlank.contig.Nterm.Delta + nFlank.contig.Sequence.ToArray().Take(nFlank.contig.Length - 3).Select(x => AminoAcid.Get(x).MonoIsotopicWeight).Sum();
			string nFlankEnd = new string(nFlank.contig.Sequence.ToArray().Skip(nFlank.contig.Length - 3).Take(3).ToArray());
			double cFlankOffset = cFlank.contig.Nterm.Delta;
			string cFlankEnd = new string(cFlank.contig.Sequence.ToArray().Take(3).ToArray());
			var key = (nFlankOffset, nFlankEnd, cFlankOffset, cFlankEnd);
			return key;
		}

		private void PopulateTemplateListView()
		{
			// get all the numbers
			var numberedTemplates = new List<(Peptide read, double[] numbering)>
			{
				(Document.CurrentBestTemplate, Document.CurrentBestTemplateNumbering),
				(Document.CurrentConsensus, Document.CurrentConsensusNumbering),
				(Document.CurrentPrediction, Document.CurrentPredictionNumbering)
			};

			numberedTemplates.AddRange(Document.NumberedTemplates.Select(x => (x.read, x.numbering)));

			// no change
			if (templateObjectListView.Objects != null && numberedTemplates.SequenceEqual(templateObjectListView.Objects.Cast<(Peptide read, double[] numbering)>()))
				return;

			var numbers = Helpers.OrderImgtNumbering(numberedTemplates.SelectMany(x => x.numbering));

			if (templateObjectListView.AllColumns.Count == 0)
			{
				Helpers.CreateColumn(templateObjectListView, "Name", HorizontalAlignment.Center, delegate (object obj)
				{
					var (template, numbering) = ((Peptide template, double[] numbering))obj;

					return template.Name;
				}, customFilterMenuBuilder: new StringFilterMenuBuilder(), objectsToBe: numberedTemplates.Select(x => (object)x));

				Helpers.CreateColumn(templateObjectListView, "Origin", HorizontalAlignment.Center, delegate (object obj)
				{
					var parsed = ((Peptide template, double[] numbering))obj;
					if (parsed == (Document.CurrentConsensus, Document.CurrentConsensusNumbering))
					{
						return "Consensus";
					}
					else if (parsed == (Document.CurrentPrediction, Document.CurrentPredictionNumbering))
					{
						return "Prediction";
					}
					else if (parsed == (Document.CurrentBestTemplate, Document.CurrentBestTemplateNumbering))
					{
						return "Rematched Template";
					}
					else
					{
						return "Template";
					}
				}, customFilterMenuBuilder: new StringFilterMenuBuilder(), objectsToBe: numberedTemplates.Select(x => (object)x));

				templateObjectListView.UseCellFormatEvents = true;
				templateObjectListView.FormatCell += TemplateObjectListView_FormatCell;

				foreach (var region in Helpers.GetAllRegionEnums())
				{
					// parse the name
					RegionType curRegion = (RegionType)region;
					if (curRegion == RegionType.None)
						continue;

					var (startImgt, endImgt) = Helpers.GetBorders(curRegion);
					var numInRange = numbers.Where(x => x.IsBetweenInclusive(startImgt, endImgt)).ToArray();
					Helpers.CreateColumn(templateObjectListView, Enum.GetName(typeof(RegionType), region), HorizontalAlignment.Left, delegate (Object obj)
					{
						(Peptide template, double[] numbering) = ((Peptide template, double[] numbering))obj;

						char[] charArray = ConvertToGappedString((template.Sequence, numbering), curRegion);
						string sequence = new string(charArray);

						if (sequence == null)
							return "Unknown";
						else
							return sequence;
					}, customFilterMenuBuilder: new StringFilterMenuBuilder(), objectsToBe: numberedTemplates.Select(x => (object)x));
				}

				templateObjectListView.AllColumns.Last().FillsFreeSpace = true;
			}

			templateObjectListView.SetObjects(numberedTemplates);

			templateObjectListView.RebuildColumns();
		}

		private void TemplateObjectListView_FormatCell(object sender, FormatCellEventArgs e)
		{
			if (e.ColumnIndex == 0)
				e.SubItem.Font = new Font(e.SubItem.Font, FontStyle.Bold);
		}

		private void DrawDotPlotForContigs(RankedContig[] contigs, Chart chart, bool isFullLength = false)
		{
			if (!contigs.Any())
				return;

			ChartArea a = new ChartArea("Default");
			a.CursorX.IsUserSelectionEnabled = true;
			a.CursorY.IsUserSelectionEnabled = true;
			//var startOffset = -0.5;
			//var endOffset = 0.5;
			//a.AxisX.LabelStyle.Angle = -45;
			//a.AxisX.IsLabelAutoFit = false;
			chart.ChartAreas.Clear();
			chart.Legends.Clear();
			chart.Series.Clear();

			chart.ChartAreas.Add(a);// Create a new legend called "Legend2".
			chart.Legends.Add(new Legend(LegendName));

			double peaksMax = contigs.Select(x => x.peaks).Max();
			double peaksMin = contigs.Select(x => x.peaks).Min();
			double templateMax = contigs.Select(x => x.template).Max();
			double templateMin = contigs.Select(x => x.template).Min();
			double spectrumMax = contigs.Select(x => x.spectrum).Max();
			double spectrumMin = contigs.Select(x => x.spectrum).Min();
			double multiMax = contigs.Select(x => x.multi).Max();
			double multiMin = contigs.Select(x => x.multiR).Min();
			double conservedMax = contigs.Select(x => -Math.Log10(x.conservedness)).Max();
			double conservedMin = contigs.Select(x => -Math.Log10(x.conservedness)).Min();

			var monoMass = Proteomics.ConvertAverageMassToMonoIsotopic(Document.Spectrum.PrecursorMass, true);
			double massDeltaMax = contigs.Select(x => Math.Abs(monoMass - (x.contig.MonoIsotopicMass - x.contig.Nterm.Delta - x.contig.Cterm.Delta))).Max();
			double massDeltaMin = contigs.Select(x => Math.Abs(monoMass - (x.contig.MonoIsotopicMass - x.contig.Nterm.Delta - x.contig.Cterm.Delta))).Min();

			// Set Docking of the Legend chart to the Default Chart Area.
			chart.Legends[LegendName].DockedToChartArea = "NotSet";
			Series peaks = new Series($"Peaks ({Math.Round(peaksMin, 3)} - {Math.Round(peaksMax, 3)})")
			{
				ChartType = SeriesChartType.Point,
				Color = PeaksColor,
				Legend = LegendName,
				IsVisibleInLegend = true
			};
			Series template = new Series($"Template ({Math.Round(templateMin, 3)} - {Math.Round(templateMax, 3)})")
			{
				ChartType = SeriesChartType.Point,
				Color = TemplateColor,
				Legend = LegendName,
				IsVisibleInLegend = true
			};
			Series spectrum = new Series($"Spectrum ({Math.Round(spectrumMin, 3)} - {Math.Round(spectrumMax, 3)})")
			{
				ChartType = SeriesChartType.Point,
				Color = SpectrumColor,
				Legend = LegendName,
				IsVisibleInLegend = true
			};
			Series conserved = new Series($"Conserved ({Math.Round(conservedMin, 3)} - {Math.Round(conservedMax, 3)})")
			{
				ChartType = SeriesChartType.Point,
				Color = ConservedColor,
				Legend = LegendName,
				IsVisibleInLegend = true
			};
			Series multi = new Series($"Multi ({Math.Round(multiMin, 3)} - {Math.Round(multiMax, 3)})")
			{
				ChartType = SeriesChartType.Point,
				Color = MultiColor,
				Legend = LegendName,
				IsVisibleInLegend = true
			};

			int i = 0;

			Series massDelta = new Series($"Mass Delta ({Math.Round(massDeltaMin, 3)} - {Math.Round(massDeltaMax, 3)})")
			{
				ChartType = SeriesChartType.Point,
				Color = Color.Black,
				Legend = LegendName,
				IsVisibleInLegend = true
			};

			foreach (var contig in contigs)
			{
				i++;
				peaks.Points.AddXY(i, (contig.peaks - peaksMin) / (peaksMax - peaksMin));
				peaks.Points.Last().ToolTip = "peaks: " + (peaks.Points.Last().YValues.First() * (peaksMax - peaksMin) + peaksMin).ToString();
				template.Points.AddXY(i, (contig.template - templateMin) / (templateMax - templateMin));
				template.Points.Last().ToolTip = "template: " + (template.Points.Last().YValues.First() * (templateMax - templateMin) + templateMin).ToString();
				spectrum.Points.AddXY(i, (contig.spectrum - spectrumMin) / (spectrumMax - spectrumMin));
				spectrum.Points.Last().ToolTip = "spectrum score: " + (spectrum.Points.Last().YValues.First() * (spectrumMax - spectrumMin) + spectrumMin).ToString();
				multi.Points.AddXY(i, (contig.multi - multiMin) / (multiMax - multiMin));
				multi.Points.Last().ToolTip = "multi score: " + (multi.Points.Last().YValues.First() * (multiMax - multiMin) + multiMin).ToString();
				conserved.Points.AddXY(i, (-Math.Log10(contig.conservedness) - conservedMin) / (conservedMax - conservedMin));
				conserved.Points.Last().ToolTip = "-log10 conservedness: " + (conserved.Points.Last().YValues.First() * (conservedMax - conservedMin) + conservedMin).ToString();
				if (isFullLength)
				{
					massDelta.Points.AddXY(i, (Math.Abs(monoMass - (contig.contig.MonoIsotopicMass - contig.contig.Nterm.Delta - contig.contig.Cterm.Delta)) - massDeltaMin) / (massDeltaMax - massDeltaMin));
					massDelta.Points.Last().ToolTip = "mass delta: " + (massDelta.Points.Last().YValues.First() * (massDeltaMax - massDeltaMin) + massDeltaMin).ToString();
				}
			}
			chart.Series.Add(peaks);
			chart.Series.Add(template);
			chart.Series.Add(spectrum);
			chart.Series.Add(conserved);
			chart.Series.Add(multi);

			if (isFullLength)
				chart.Series.Add(massDelta);
		}

		private void Draw2DPlot(RankedContig[] contigs, Chart chart)
		{
			if (!contigs.Any())
				return;

			ChartArea a = new ChartArea("Default");
			a.CursorX.IsUserSelectionEnabled = true;
			a.CursorY.IsUserSelectionEnabled = true;

			chart.ChartAreas.Clear();
			chart.Legends.Clear();
			chart.Series.Clear();
			a.AxisY.Minimum = contigs.Select(x => x.multi).Min();

			chart.ChartAreas.Add(a);// Create a new legend called "Legend2".
			chart.Legends.Add(new Legend(LegendName));

			// Set Docking of the Legend chart to the Default Chart Area.
			chart.Legends[LegendName].DockedToChartArea = "NotSet";

			int i = 0;

			Series massDelta = new Series($"Candidate scores)")
			{
				ChartType = SeriesChartType.Point,
				Color = Color.FromArgb(80, 0, 0, 0),
				Legend = LegendName,
				IsVisibleInLegend = false,
			};

			massDelta.LabelToolTip = "Overall ranking";
			massDelta.Font = new Font("Consolas", 14f);
			massDelta.SmartLabelStyle = new SmartLabelStyle()
			{
				Enabled = true,
				IsMarkerOverlappingAllowed = false,
				IsOverlappedHidden = true,
			};

			chart.ChartAreas[0].AxisX.Title = "Spectrum";
			chart.ChartAreas[0].AxisY.Title = "K-mer";

			foreach (var contig in contigs)
			{
				massDelta.Points.AddXY((int)contig.spectrum, contig.multi);
				massDelta.Points.Last().ToolTip = $"kmer#{contig.multiR}: {massDelta.Points.Last().YValues.First()} || Spectrum#{contig.spectrumR}: {massDelta.Points.Last().XValue} \n\n#{i}: {contig.contig.Sequence}";
				i++;
				if (i < 5)
				{
					massDelta.Points.Last().Label = $"#{i}";
				}
			}
			chart.Series.Add(massDelta);
		}

		#endregion

		#region Events

		private void TabToWindow(object sender, MouseEventArgs e)
		{
			var tc = (TabControl)sender;

			Rectangle r = tc.GetTabRect(tc.SelectedIndex);
			if (r.Contains(e.Location))
			{
				TabPage CurrentTab = tc.SelectedTab;
				Form newWindow = new Form();
				foreach (Control ctrl in CurrentTab.Controls)
				{
					// charts are annoying we have to handle them separately
					if (ctrl.GetType() != typeof(Chart)/* && ctrl.GetType() != typeof(SplitContainer)*/)
					{
						var clonedControl = ctrl;
						newWindow.Controls.Add(clonedControl);
						clonedControl.Parent = newWindow;
						clonedControl.Dock = ctrl.Dock;
					}
					else if (ctrl.GetType() == typeof(SplitContainer))
					{
						SplitContainer newSplitter = new SplitContainer()
						{
							Dock = DockStyle.Fill
						};
						foreach (Control item in ctrl.Controls[0].Controls)
						{
							newSplitter.Panel1.Controls.Add(Clone(item));
						}
						foreach (Control item in ctrl.Controls[1].Controls)
						{
							newSplitter.Panel2.Controls.Add(Clone(item));
						}

						newWindow.Controls.Add(newSplitter);
					}
				}

				if (ControlsWithDockedCharts.TryGetValue(CurrentTab, out Chart val))
				{
					Chart clonedChart = CloneChart(val);
					clonedChart.Parent = newWindow;
					clonedChart.Dock = val.Dock;
					newWindow.Controls.Add(clonedChart);
				}

				newWindow.Show();
			}
		}

		private void MainTabControl_MouseUp(object sender, MouseEventArgs e)
		{
			//Looping through the controls.
			for (int i = 1; i < this.mainTabControl.TabPages.Count; i++)
			{
				Rectangle r = mainTabControl.GetTabRect(i);

				//Getting the position of the "x" mark.
				Rectangle closeButton = new Rectangle(r.Right - 15, r.Top + 4, 9, 7);
				if (closeButton.Contains(e.Location) && i != 0)
				{
					mainTabControl.TabPages.RemoveAt(i);
					break;
				}
			}
		}

		private void MainTabControl_DrawItem(object sender, DrawItemEventArgs e)
		{
			if (e.Index != 0)
			{
				e.Graphics.DrawString("x", e.Font, Brushes.Black, e.Bounds.Right - 15, e.Bounds.Top + 4);
			}

			e.Graphics.DrawString(mainTabControl.TabPages[e.Index].Text, e.Font, Brushes.Black, e.Bounds.Left + 12, e.Bounds.Top + 4);
			e.DrawFocusRectangle();
		}

		private void templateObjectListView_CellRightClick(object sender, CellRightClickEventArgs e)
		{
			var olv = ((ObjectListView)sender);
			olv.ContextMenu.Show(olv, new Point(e.Location.X, e.Location.Y));
		}

		private void ScoreOlv_FormatRow(object sender, FormatRowEventArgs e)
		{
			ColourSelected(sender, e);
		}

		private void Olv_FormatRow(object sender, FormatRowEventArgs e)
		{
			ColourSelected(sender, e);
		}

		private void ColourSelected(object sender, FormatRowEventArgs e)
		{
			var olv = (ObjectListView)sender;

			var reg = (RegionType)olv.Tag;
			if (SelectedContigs.ContainsKey(reg) && ((RankedContig)e.Model).contig == SelectedContigs[reg].contig)
			{
				e.Item.BackColor = Color.LightGreen;
			}
		}

		private void PropertyGrid_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
		{
			if (Document != null)
			{
				Document.CurrentSettings = (Document.Settings)((PropertyGrid)s).SelectedObject;
			}
			else
			{
				_settings = (Document.Settings)((PropertyGrid)s).SelectedObject;
			}
		}

		private void repopulateCdrToolStripMenuItem_Click(object sender, EventArgs e)
		{
			recombineAll();
		}

		private ObjectListView recombineAll()
		{
			List<List<RankedContig>> toRecomb = new List<List<RankedContig>>()
			{
				GetOlv(RegionType.FR1).CheckedObjects.Cast<RankedContig>().ToList(),
				GetOlv(RegionType.FR2).CheckedObjects.Cast<RankedContig>().ToList(),
				GetOlv(RegionType.FR3).CheckedObjects.Cast<RankedContig>().ToList(),
				GetOlv(RegionType.FR4).CheckedObjects.Cast<RankedContig>().ToList(),
			};

			var combs = HeckLib.utils.Extensions.Recombine(toRecomb);
			var viableCombos = new List<(Peptide seq, List<RankedContig> components, double[] numbering)>();

			foreach (var combo in combs)
			{
				var fr1 = combo[0];
				var fr2 = combo[1];
				var fr3 = combo[2];
				var fr4 = combo[3];

				FillIfCdr(RegionType.CDR1, fr1, fr2);
				FillIfCdr(RegionType.CDR2, fr2, fr3);
				FillIfCdr(RegionType.CDR3, fr3, fr4);

				// make the regions to recombine
				List<List<RankedContig>> CdrstoRecomb = new List<List<RankedContig>>()
				{
					GetSessionClippedContigs(RegionType.CDR1).Take(Settings.MaxCdrInPrediction).ToList(),
					GetSessionClippedContigs(RegionType.CDR2).Take(Settings.MaxCdrInPrediction).ToList(),
					GetSessionClippedContigs(RegionType.CDR3).Take(Settings.MaxCdrInPrediction).ToList(),
				};

				for (int i = 0; i < CdrstoRecomb.Count; i++)
				{
					if (CdrstoRecomb[i].Count == 0)
					{
						CdrstoRecomb[i].Add(new RankedContig()
						{
							contig = new Peptide(""),
							numbering = new double[0],
							origin = SequenceSource.Reads,
						});
					}
				}

				var cdrCombs = HeckLib.utils.Extensions.Recombine(CdrstoRecomb);

				double precursorMass = Proteomics.ConvertAverageMassToMonoIsotopic(Document.Spectrum.PrecursorMass, true);

				foreach (var cdrCombo in cdrCombs)
				{
					//var cdr1 = cdrCombo[0];
					//var cdr2 = cdrCombo[1];
					//var cdr3 = cdrCombo[2];

					List<double> numbering = new List<double>();
					List<RankedContig> contigs = new List<RankedContig>();
					StringBuilder seqBuilder = new StringBuilder();

					for (int i = 0; i < cdrCombo.Count; i++)
					{
						seqBuilder.Append(combo[i].contig.Sequence);
						seqBuilder.Append(cdrCombo[i].contig.Sequence);
						numbering.AddRange(combo[i].numbering);
						numbering.AddRange(cdrCombo[i].numbering);
						contigs.Add(combo[i]);
						contigs.Add(cdrCombo[i]);
					}
					seqBuilder.Append(combo[3].contig.Sequence);
					numbering.AddRange(combo[3].numbering);
					contigs.Add(combo[3]);

					if (seqBuilder.Length != numbering.Count)
					{

						//TODO emergency fix
						continue;
					}

					var prediction = new Peptide()
					{
						Nterm = fr1.contig.Nterm,
						Sequence = seqBuilder.ToString(),
						Name = string.Join(" ", numbering),
						Cterm = fr4.contig.Cterm,
						Modifications = new Modification[seqBuilder.Length],
					};

					if (Math.Abs(precursorMass - prediction.MonoIsotopicMass) < Settings.ToleranceInDaGapFillers)
					{
						//lock (viableCombos)
						//{
						viableCombos.Add((prediction, contigs, numbering.ToArray()));
						//}
					}
				}
			}

			// put the old contigs back
			FillIfCdr(RegionType.CDR1);
			FillIfCdr(RegionType.CDR2);
			FillIfCdr(RegionType.CDR3);

			var modifications = Modification.Parse();
			(Peptide seq, double[] numbering)[] viableCombos2 = viableCombos.Select(x =>
			{

				if (Settings.Cdr3Bridges)
				{
					if (x.numbering.Contains(104))
					{
						var idx104 = x.numbering.IndexOf(104);
						x.seq.Modifications[idx104] = modifications["Hydrogen loss"];
					}

					if (x.numbering.Contains(154))
					{
						var idx154 = x.numbering.IndexOf(154);
						x.seq.Modifications[idx154] = modifications["Hydrogen loss"];
					}
				}

				return (x.seq, x.numbering);
			}).ToArray();
			var range = OLVDoublePrompt.ShowDialog(0, viableCombos2.Select(x => x.seq.Length).Max()).Select(x => (int)x).ToArray();

			var ranked = Helpers.RankFullLength(viableCombos2, SequenceSource.Contig, Document, Document.Locus, range);
			Document.Predictions = ranked;
			if (ranked.Where(x => x.contig == null).Any())
			{
				var empty = ranked.Where(x => x.contig == null);
				var c = empty.Count();
				var minRank = empty.Select(x => x.sumR).Min();
				foreach (var item in empty)
				{
					Debug.WriteLine($"Sumr: {item.sumR}, peaksScore: {item.peaks}, spectrumScore: {item.spectrum}");
				}
				MessageBox.Show($"Found and discarded empty contigs (N = {c}, minrank = {minRank})");
				Document.Predictions = ranked.Where(x => x.contig != null).ToArray();
			}
			return ShowPredictionsView(null, range);
		}

		private void RecombineAdjacent(object sender, EventArgs e)
		{
			var cm = (MenuItem)sender;
			var olv = (ObjectListView)cm.GetContextMenu().SourceControl;

			RegionType region = (RegionType)olv.Tag;
			RecombineAdjacentInner(region);
		}

		private ObjectListView RecombineAdjacentInner(RegionType region)
		{
			if (!Helpers.IsCdr(region))
				return null;

			var flanking = Helpers.GetFlanking(region);

			List<List<RankedContig>> toRecomb = new List<List<RankedContig>>()
			{
				GetOlv(flanking.Item1).CheckedObjects.Cast<RankedContig>().ToList(),
				GetOlv(flanking.Item2).CheckedObjects.Cast<RankedContig>().ToList(),
			};

			if (toRecomb.Where(x => !x.Any()).Any())
			{
				MessageBox.Show("no contigs checked for recombination");
				return null;
			}
			var combs = HeckLib.utils.Extensions.Recombine(toRecomb);
			var viableCombos = new List<(Peptide seq, List<RankedContig> components, double[] numbering)>();
			foreach (var combo in combs)
			{
				var fr1 = combo[0];
				var fr2 = combo[1];

				// make the regions to recombine
				FillIfCdr(region, fr1, fr2);
				List<List<RankedContig>> CdrstoRecomb = new List<List<RankedContig>>()
				{
					GetSessionClippedContigs(region).Take(Settings.MaxCdrInPrediction).ToList(),
				};

				for (int i = 0; i < CdrstoRecomb.Count; i++)
				{
					if (CdrstoRecomb[i].Count == 0)
					{
						CdrstoRecomb[i].Add(new RankedContig()
						{
							contig = new Peptide(""),
							numbering = new double[0],
							origin = SequenceSource.Reads,
						});
					}
				}

				var cdrCombs = HeckLib.utils.Extensions.Recombine(CdrstoRecomb);

				double precursorMass = Proteomics.ConvertAverageMassToMonoIsotopic(Document.Spectrum.PrecursorMass, true);

				foreach (var cdrCombo in cdrCombs)
				{
					List<double> numbering = new List<double>();
					List<RankedContig> contigs = new List<RankedContig>();
					StringBuilder seqBuilder = new StringBuilder();

					for (int i = 0; i < cdrCombo.Count; i++)
					{
						seqBuilder.Append(combo[i].contig.Sequence);
						seqBuilder.Append(cdrCombo[i].contig.Sequence);
						numbering.AddRange(combo[i].numbering);
						numbering.AddRange(cdrCombo[i].numbering);
						contigs.Add(combo[i]);
						contigs.Add(cdrCombo[i]);
					}
					seqBuilder.Append(combo[cdrCombo.Count].contig.Sequence);
					numbering.AddRange(combo[cdrCombo.Count].numbering);
					contigs.Add(combo[cdrCombo.Count]);

					if (seqBuilder.Length != numbering.Count)
					{

						//TODO emergency fix
						continue;
					}

					var prediction = new Peptide()
					{
						Nterm = fr1.contig.Nterm,
						Sequence = seqBuilder.ToString(),
						Name = string.Join(" ", numbering),
						Cterm = fr2.contig.Cterm,
						Modifications = new Modification[seqBuilder.Length],
					}.ShiftPeptideToOptimum(Document.Spectrum, Document.ScoringModel);

					if (Math.Abs(precursorMass - prediction.MonoIsotopicMass) < Settings.ToleranceInDaGapFillers)
					{
						viableCombos.Add((prediction, contigs, numbering.ToArray()));
					}
				}
			}
			var modifications = Modification.Parse();
			(Peptide seq, double[] numbering)[] viableCombos2 = viableCombos.Select(x =>
			{
				if (Document.CurrentSettings.Cdr3Bridges)
				{
					if (x.numbering.Contains(104))
					{
						var idx104 = x.numbering.IndexOf(104);
						x.seq.Modifications[idx104] = modifications["Hydrogen loss"];
					}

					if (x.numbering.Contains(154))
					{
						var idx154 = x.numbering.IndexOf(154);
						x.seq.Modifications[idx154] = modifications["Hydrogen loss"];
					}
				}

				return (x.seq, x.numbering);
			}).ToArray();
			var range = OLVDoublePrompt.ShowDialog(0, viableCombos2.Select(x => x.seq.Length).Max()).Select(x => (int)x).ToArray();

			var ranked = Helpers.RankFullLength(viableCombos2, SequenceSource.Contig, Document, Document.Locus, range);

			if (ranked.Where(x => x.contig == null).Any())
			{
				var empty = ranked.Where(x => x.contig == null);
				var c = empty.Count();
				var minRank = empty.Select(x => x.sumR).Min();
				foreach (var item in empty)
				{
					Debug.WriteLine($"Sumr: {item.sumR}, peaksScore: {item.peaks}, spectrumScore: {item.spectrum}");
				}
				MessageBox.Show($"Found and discarded empty contigs (N = {c}, minrank = {minRank})");
				ranked = ranked.Where(x => x.contig != null).ToArray();
			}

			FillIfCdr(region);
			

			return ShowPredictionsView(ranked, range);
		}


		/// <summary>
		/// color the current prediction
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void TemplateObjectListView_FormatRow(object sender, FormatRowEventArgs e)
		{
			if (e.RowIndex == 0 || e.RowIndex == 1)
			{
				e.Item.BackColor = Color.SkyBlue;
			}
			if (e.RowIndex == 2)
			{
				e.Item.BackColor = Color.Pink;
			}
			if (e.RowIndex == 3)
			{
				e.Item.BackColor = Color.Khaki;
			}
		}

		private void Olv_ItemChecked(object sender, ItemCheckedEventArgs e)
		{

		}

		private void SetCurrentSelection(RegionType curRegion, RankedContig checkedContig)
		{
			if (!SelectedContigs.TryGetValue(curRegion, out RankedContig _))
				SelectedContigs.Add(curRegion, checkedContig);
			else
				SelectedContigs[curRegion] = checkedContig;
		}

		private void FillIfCdr(RegionType curRegion)
		{
			if (!Helpers.IsCdr(curRegion))
				return;

			var (nFlankRegion, cFlankRegion) = Helpers.GetFlanking(curRegion);

			var nFlankIsEmpty = !SelectedContigs.TryGetValue(nFlankRegion, out RankedContig nFlank);
			var cFlankIsEmpty = !SelectedContigs.TryGetValue(cFlankRegion, out RankedContig cFlank);

			if (nFlankIsEmpty || cFlankIsEmpty)
				return;

			var contigs = FillersFromFlanks(nFlank, cFlank, curRegion);

			var sourcesToDisplay = Settings.SequenceSourceToDisplay.Where(x => x.Value).Select(x => x.Key);

			contigs = contigs.Where(x => sourcesToDisplay.Contains(x.origin)).ToArray();

			var varsToOrderOn = Document.CurrentSettings.OrderingVars[curRegion];
			if (varsToOrderOn.Contains(5) && !Document.CurrentSettings.UseMultiScore)
			{
				var l = varsToOrderOn.ToList();
				l.Add(1);
				varsToOrderOn = l.ToArray();
			}
			contigs = Document.Reorder(contigs, varsToOrderOn);

			SetSessionClippedContigs(curRegion, contigs);
		}

		private void FillIfCdr(RegionType curRegion, RankedContig nFlank, RankedContig cFlank)
		{
			if (!Helpers.IsCdr(curRegion))
				return;

			var (nFlankRegion, cFlankRegion) = Helpers.GetFlanking(curRegion);
			
			var contigs = FillersFromFlanks(nFlank, cFlank, curRegion);

			var sourcesToDisplay = Settings.SequenceSourceToDisplay.Where(x => x.Value).Select(x => x.Key);

			contigs = contigs.Where(x => sourcesToDisplay.Contains(x.origin)).ToArray();

			var varsToOrderOn = Document.CurrentSettings.OrderingVars[curRegion];
			if (varsToOrderOn.Contains(5) && !Document.CurrentSettings.UseMultiScore)
			{
				var l = varsToOrderOn.ToList();
				l.Add(1);
				varsToOrderOn = l.ToArray();
			}
			contigs = Document.Reorder(contigs, varsToOrderOn);

			SetSessionClippedContigs(curRegion, contigs);
		}

		private void DrawPeaksSupportChart(RegionType curRegion)
		{
			if (Document.CurrentPrediction.Sequence == "")
			{
				return;
			}

			double floor = Helpers.GetBorders(curRegion).Item1;
			double ceiling = Helpers.GetBorders(curRegion).Item2;
			(PeaksPeptideData Peptide, double[] numbering)[] reads = Document.NumberedReads.Where(x =>
			{
				return x.numbering.Min().IsBetweenInclusive(floor, ceiling) || x.numbering.Max().IsBetweenInclusive(floor, ceiling) ||
					(x.numbering.Min() <= floor && x.numbering.Max() >= ceiling); // spans the region
			}).Select(x => (x.read, x.numbering)).ToArray();

			var numbered = Document.ReadFilter.GetNumberingAndAlignmentForReadsDynamicProgramming(reads.Select(x => x.Peptide).ToArray(), Document.CurrentPrediction).Select(x => (x.read, x.numbering)).ToList();
			var consensus = ReadFilter.GetConsensusFromReadsOriginal(numbered).Where(x => x.frequencies.Count != 0).ToList();

			double[] numbering = new double[0];
			var sequence = "";

			if (SelectedContigs.TryGetValue(curRegion, out RankedContig middle))
			{
				numbering = middle.numbering;
				sequence = middle.contig.Sequence.Replace("I", "L");
			}
			else
			{
				GetConsensusContig(curRegion, out numbering, out sequence);
			}

			var chart = GetContigPeaksChart(curRegion);
			DrawConsensusChart(consensus, numbering, sequence, chart);
		}

		private void DrawMultiSupportChart(RegionType curRegion)
		{
			if (Document.CurrentPrediction.Sequence == "")
			{
				return;
			}

			double floor = Helpers.GetBorders(curRegion).Item1;
			double ceiling = Helpers.GetBorders(curRegion).Item2;
			(PeaksPeptideData Peptide, double[] numbering)[] reads = Document.NumberedReads.Where(x =>
			{
				return x.numbering.Min().IsBetweenInclusive(floor, ceiling) || x.numbering.Max().IsBetweenInclusive(floor, ceiling) ||
					(x.numbering.Min() <= floor && x.numbering.Max() >= ceiling); // spans the region
			}).Select(x => (x.read, x.numbering)).ToArray();

			var numbered = Document.ReadFilter.GetNumberingAndAlignmentForReadsDynamicProgramming(reads.Select(x => x.Peptide).ToArray(), Document.CurrentPrediction).Select(x => (x.read.Peptide, x.numbering)).ToArray();

			double[] numbering = new double[0];
			var sequence = "";

			if (SelectedContigs.TryGetValue(curRegion, out RankedContig middle))
			{
				numbering = middle.numbering;
				sequence = middle.contig.Sequence;

				if (Helpers.IsCdr(curRegion))
				{
					// we want to add some adjacent residues
					var flanking = Helpers.GetFlanking(curRegion);
					if (SelectedContigs.TryGetValue(flanking.Item1, out RankedContig fr1))
					{
						var nums = fr1.numbering.Last(6).ToList();
						nums.AddRange(numbering);

						var seqFr1 = new string(fr1.contig.Sequence.Last(6).ToArray());
						seqFr1 += sequence;

						numbering = nums.ToArray();
						sequence = seqFr1;
					}

					if (SelectedContigs.TryGetValue(flanking.Item2, out RankedContig fr2))
					{
						var nums = numbering.ToList();
						nums.AddRange(fr2.numbering.Take(6));

						var seqFr2 = sequence;
						seqFr2 += fr2.contig.Sequence.Substring(0, 6);

						numbering = nums.ToArray();
						sequence = seqFr2;
					}
				}
			}
			else
			{
				GetConsensusContig(curRegion, out numbering, out sequence);
			}

			var chart = GetContigMultiChart(curRegion);
			try
			{
				DrawMultiChart(GetMultiSupportScore((sequence.Replace('I', 'L')
					//.Replace("Q", "K")
					, numbering), numbered), numbering, sequence, chart);
			}
			catch (Exception)
			{

			}
		}

		public static List<List<Coverage>> GetMultiSupportScore((string sequence, double[] numbering) target, (string sequence, double[] numbering)[] reads, int tolerance = 7)
		{

			// one point per residue per hit. a residue in a subseq of 5 gets 5 + 4 + 3 + 2 + 1 points
			var matchesPerSizePerRes = new Dictionary<int, Dictionary<int, List<((string, double[]), (int, string))>>>();
			(int resIdx, int matchSize)[] pepIdxLen = new (int, int)[reads.Length];
			var innerReads = reads.Select((x, i) => (x.sequence, x.numbering, i)).ToArray();
			int size = 6;

			var numbering = target.numbering;

			foreach (var read in innerReads)
			{
				int startIdx = -1;
				int endIdx = -1;

				var queryFloor = read.numbering.Min();
				var queryCeiling = read.numbering.Max();

				for (int idx = 0; idx < numbering.Length; idx++)
				{
					if (numbering[idx] > queryFloor - 0.001 - tolerance)
					{
						startIdx = idx;
						break;
					}
				}

				for (int idx = numbering.Length - 1; idx > 0; idx--)
				{
					if (numbering[idx] < queryCeiling + 0.001 + tolerance)
					{
						endIdx = idx;
						break;
					}
				}

				if (!(startIdx != -1) || !(endIdx != -1))
				{
					//read does not reach the query
					continue;
				}

				var targetSeq = target.sequence.Substring(startIdx, endIdx - startIdx + 1);
				bool hit = false;
				for (int currentSize = read.sequence.Length; currentSize >= size; currentSize--)
				{
					for (int currentIdx = 0; currentIdx <= read.sequence.Length - currentSize; currentIdx++)
					{
						var query = read.sequence.Substring(currentIdx, currentSize);
						var idxOf = targetSeq.IndexOf(query);
						if (idxOf != -1)
						{
							pepIdxLen[read.i] = (startIdx + idxOf, currentSize);
							hit = true;
							break;
						}
					}
					if (hit)
						break;
				}
			}

			List<List<Coverage>> coverages = new List<List<Coverage>>();


			for (int i = 0; i < target.sequence.Length; i++)
			{
				coverages.Add(new List<Coverage>());
			}
			for (int j = 0; j < pepIdxLen.Length; j++)
			{
				var (resIdx, matchSize) = pepIdxLen[j];
				if (matchSize != 0)
				{
					int idx = resIdx;
					int n = 0;
					int c = matchSize;
					for (int i = 0; i < matchSize; i++)
					{
						coverages[idx + i].Add(new Coverage
						{
							N = n + i + 1,
							C = c - i,
							Idx = idx + i,
							Imgt = target.numbering[idx + i],
						});
					}
				}
			}

			return coverages;
		}

		private void DrawSpectrumSupportChart(RegionType curRegion)
		{
			var chart = GetContigSpectrumChart(curRegion);
			chart.ChartAreas.Clear();
			chart.Series.Clear();
			chart.Legends.Clear();

			try
			{
				if (SelectedContigs.TryGetValue(curRegion, out RankedContig middle))
				{
					var asm = new AnnotatedSpectrumMatch(Document.Spectrum, middle.contig, Document.AnnotationModel);

					chart.Legends.Add(new Legend(LegendName));
					chart.Legends[LegendName].Enabled = false;
					ChartArea a = new ChartArea();
					var startOffset = -0.5;
					var endOffset = 0.5;
					//a.AxisX.Minimum = -1;
					//a.AxisX.Maximum = middle.numbering.Count();
					a.AxisX.LabelStyle.Angle = -45;
					a.AxisX.IsLabelAutoFit = false;
					chart.Legends[LegendName].DockedToChartArea = "NotSet";
					//a.AxisY.Minimum = 0;
					//a.AxisY.Maximum = 1;
					chart.ChartAreas.Add(a);

					var maxInt = (double)Document.Spectrum.Peaks.Select(x => x.Intensity).Max();
					var sumInt = (double)Document.Spectrum.Peaks.Select(x => x.Intensity).Sum();

					var matchesWithIntensity = asm.FragmentMatches.Select((x, i) =>
					{
						float intensity = asm.Spectrum.Peaks[i].Intensity;

						return (Intensity: intensity, position: x != null ? x.Position : -1, terminus: x == null ? Proteomics.Terminus.None : x.Terminus, x, asm.Spectrum.Peaks[i]);
					}).ToList();

					var nSeries = new Series("N Terminal");
					var cSeries = new Series("C Terminal");
					var maxIntN = 0f;
					var maxIntC = 0f;

					int j = 0;
					foreach (var item in asm.Match.Sequence)
					{
						var res = middle.contig.Sequence[j];
						CustomLabel label = new CustomLabel(startOffset, endOffset, new string(new char[] { res }), 0, LabelMarkStyle.None);
						a.AxisX.CustomLabels.Add(label);
						startOffset++;
						endOffset++;
						j++;
						IEnumerable<(float Intensity, int position, Proteomics.Terminus terminus, PeptideFragment x, HeckLib.Centroid)> nMatches = matchesWithIntensity.Where(y => y.position == j && y.terminus == Proteomics.Terminus.N);
						IEnumerable<float> matchedPeakIntensitiesN = nMatches.Select(y => y.Intensity).ToArray();
						IEnumerable<(float Intensity, int position, Proteomics.Terminus terminus, PeptideFragment x, HeckLib.Centroid)> cMatches = matchesWithIntensity.Where(y => y.position == j && y.terminus == Proteomics.Terminus.C);
						IEnumerable<float> matchedPeakIntensitiesC = cMatches.Select(y => y.Intensity).ToArray();

						float maxIntMatchedC = matchedPeakIntensitiesC.Any() ? matchedPeakIntensitiesC.Max() : 0.00f;
						float maxIntMatchedN = matchedPeakIntensitiesN.Any() ? matchedPeakIntensitiesN.Max() : 0.00f;

						if (maxIntN < maxIntMatchedN)
							maxIntN = maxIntMatchedN;

						if (maxIntC < maxIntMatchedC)
							maxIntC = maxIntMatchedC;
					}
					j = 0;
					foreach (var item in asm.Match.Sequence)
					{
						j++;
						IEnumerable<(float Intensity, int position, Proteomics.Terminus terminus, PeptideFragment x, HeckLib.Centroid)> nMatches = matchesWithIntensity.Where(y => y.position == j && y.terminus == Proteomics.Terminus.N);
						IEnumerable<float> matchedPeakIntensitiesN = nMatches.Select(y => y.Intensity).ToArray();
						IEnumerable<(float Intensity, int position, Proteomics.Terminus terminus, PeptideFragment x, HeckLib.Centroid)> cMatches = matchesWithIntensity.Where(y => y.position == j && y.terminus == Proteomics.Terminus.C);
						IEnumerable<float> matchedPeakIntensitiesC = cMatches.Select(y => y.Intensity).ToArray();

						//double relMatchedIntSumC = matchedPeakIntensitiesC.Any() ? matchedPeakIntensitiesC.Sum() : 0.00;
						//double relMatchedIntMaxC = matchedPeakIntensitiesC.Any() ? matchedPeakIntensitiesC.Max() : 0.00;
						float maxIntMatchedC = matchedPeakIntensitiesC.Any() ? matchedPeakIntensitiesC.Max() : 0.00f;
						//double relMatchedIntSumN = matchedPeakIntensitiesN.Any() ? matchedPeakIntensitiesN.Sum() : 0.00;
						//double relMatchedIntMaxN = matchedPeakIntensitiesN.Any() ? matchedPeakIntensitiesN.Max() : 0.00;
						float maxIntMatchedN = matchedPeakIntensitiesN.Any() ? matchedPeakIntensitiesN.Max() : 0.00f;
						double relMatchedIntMaxN = matchedPeakIntensitiesN.Any() ? maxIntMatchedN / maxIntN : 0;
						double relMatchedIntMaxC = matchedPeakIntensitiesC.Any() ? maxIntMatchedC / maxIntC : 0;

						nSeries.Points.AddXY(j, relMatchedIntMaxN == 0 ? 0 : 0.1 + relMatchedIntMaxN);
						cSeries.Points.AddXY(j, relMatchedIntMaxC == 0 ? 0 : -0.1 - relMatchedIntMaxC);

						nSeries.Points.Last().ToolTip = relMatchedIntMaxN == 0 ? "" : Math.Round(nMatches.OrderBy(x => x.Intensity).First().Item5.Mz, 3).ToString();
						cSeries.Points.Last().ToolTip = relMatchedIntMaxC == 0 ? "" : Math.Round(cMatches.OrderBy(x => x.Intensity).First().Item5.Mz, 3).ToString();
					}


					nSeries.Color = Color.FromArgb((int)(255 * (0.5 + (0.5 * (maxIntN / maxInt)))), Color.Blue);
					cSeries.Color = Color.FromArgb((int)(255 * (0.5 + (0.5 * (maxIntC / maxInt)))), Color.Red);
					nSeries.Legend = LegendName;
					nSeries.IsVisibleInLegend = true;
					cSeries.Legend = LegendName;
					cSeries.IsVisibleInLegend = true;

					chart.Series.Add(cSeries);
					chart.Series.Add(nSeries);
				}
			}
			catch (Exception err)
			{
				chart.ChartAreas.Clear();
				chart.Series.Clear();
				chart.Legends.Clear();
				Log.Logger.Log(NLog.LogLevel.Trace, $"Could not draw spectrum support chart. Error: {err.Message}");
			}
		}

		private void DrawMultiChart(List<List<Coverage>> coverages, double[] numbering, string sequence, Chart chart)
		{
			chart.ChartAreas.Clear();
			chart.Series.Clear();
			chart.Legends.Clear();

			try
			{
				chart.Legends.Add(new Legend(LegendName));
				chart.Legends[LegendName].Enabled = false;
				ChartArea a = new ChartArea();
				var startOffset = -0.5;
				var endOffset = 0.5;
				a.CursorX.IsUserSelectionEnabled = true;
				a.CursorY.IsUserSelectionEnabled = true;
				a.AxisX.LabelStyle.Angle = -45;
				a.AxisX.IsLabelAutoFit = false;
				chart.Legends[LegendName].DockedToChartArea = "NotSet";
				chart.ChartAreas.Add(a);

				var xs = Enumerable.Range(0, numbering.Length).ToArray();

				Colormap colormapC = new Colormap(Color.Red, Color.Yellow, 256);
				Color Green = Color.FromArgb(0, 255, 255);
				Colormap colormapN = new Colormap(Color.Blue, Green, 256);

				var avgs = coverages.Select(x => x.Any() ? (x.Select(y => y.C).Average(), x.Select(y => y.N).Average()) : (0, 0)).ToArray();
				ColorMapper colorMapperC = new ColorMapper(colormapC, avgs.Select(x => x.Item1).Max());
				ColorMapper colorMapperN = new ColorMapper(colormapN, avgs.Select(x => x.Item2).Max());


				bool isCterminus = true;
				var cSeries = new Series("C-terminal support");
				var nSeries = new Series("N-terminal support");
				var labelSeries = new Series("Labels");
				labelSeries.IsVisibleInLegend = false;
				
				nSeries.SmartLabelStyle = new SmartLabelStyle() {
					IsMarkerOverlappingAllowed = true,
					CalloutStyle = LabelCalloutStyle.Box,
					
				};

				string seq = sequence.Substring(coverages.First().First().Idx, coverages.Count);

				for (int resPos = 0; resPos < seq.Length; resPos++)
				{
					List<Coverage>[] covs = xs.Select(x => x == resPos ? coverages[x] : new List<Coverage>()).ToArray();
					int[][] relCovs = null;

					string lab = "";

					if (isCterminus)
					{
						relCovs = covs.
							Select(x => x.Where(y => y.C != 0).Select(y => y.C).ToArray()).ToArray();

						lab = $"C-terminal support at {resPos}";
					}
					else
					{
						relCovs = covs.Select(x => x.Where(y => y.N != 0).Select(y => y.N).ToArray()).ToArray();
						lab = $"N-terminal coverage score (weighted by total length) by residues {resPos}";
					}

					List<Coverage> relevantCov = coverages[resPos];
					int yC = relevantCov.Any() ? relevantCov.Select(x => x.C).Sum() * -1 : 0;
					int yN = relevantCov.Any() ? relevantCov.Select(x => x.N).Sum() : 0;
					double meanC = Math.Round(relevantCov.Any() ? relevantCov.Select(x => x.C).Average() : 0, 2);
					double meanN = Math.Round(relevantCov.Any() ? relevantCov.Select(x => x.N).Average() : 0, 2);

					double numPeps = relevantCov.Count();
					Color colorC = colorMapperC.GetColor(meanC);
					Color colorN = colorMapperN.GetColor(meanN);

					nSeries.Points.AddXY(resPos, yN);
					nSeries.Points.Last().ToolTip = $"depth: {numPeps}\navg length: {meanN}\nscore: {meanN * numPeps}";
					nSeries.Points.Last().Color = meanN == 1 ? Color.Black : colorN;
					nSeries.Points.Last().BackGradientStyle = GradientStyle.TopBottom;
					nSeries.Points.Last().BackSecondaryColor = meanN == 1 ? Color.Black : Color.Blue;
					if (meanN == 1)
					{
						labelSeries.Points.AddXY(resPos, coverages.Select(x => x.Select(y => y.Sum).Sum()).Max() / 10 + yN);
						labelSeries.Points.Last().Color = Color.FromArgb(0, 1, 1, 1);
						labelSeries.Points.Last().Label = "N-Break";
					}
					
					cSeries.Points.AddXY(resPos, yC);
					cSeries.Points.Last().ToolTip = $"depth: {numPeps}\navg length: {meanC}\nscore: {meanC * numPeps}";
					cSeries.Points.Last().Color = meanC == 1 ? Color.Black : Color.Red;
					cSeries.Points.Last().BackSecondaryColor = meanC == 1 ? Color.Black : colorC;
					cSeries.Points.Last().BackGradientStyle = GradientStyle.TopBottom;
					if (meanC == 1)
					{
						labelSeries.Points.AddXY(resPos, coverages.Select(x => x.Select(y => y.Sum).Sum()).Max() / -10 + yC);
						labelSeries.Points.Last().Color = Color.FromArgb(0, 1, 1, 1);
						labelSeries.Points.Last().Label = "C-Break";
						labelSeries.Points.Last().LabelAngle = -90;
					}
				}

				nSeries.Legend = LegendName;
				nSeries.IsVisibleInLegend = false;
				nSeries.ChartType = SeriesChartType.StackedColumn;
				nSeries.Color = Color.Blue;
				cSeries.Legend = LegendName;
				cSeries.IsVisibleInLegend = false;
				cSeries.ChartType = SeriesChartType.StackedColumn;
				cSeries.Color = Color.Red;
				nSeries.BackSecondaryColor = Green;
				cSeries.BackSecondaryColor = Color.Yellow;
				nSeries.BackGradientStyle = GradientStyle.LeftRight;
				cSeries.BackGradientStyle = GradientStyle.LeftRight;

				nSeries.LegendText = "N terminal support";
				nSeries.LegendToolTip = "N term (correct residues on right)";
				cSeries.LegendText = "C terminal support";
				cSeries.LegendToolTip = "C term (correct residues on left)";

				chart.Series.Add(nSeries);
				chart.Series.Add(cSeries);
				chart.Series.Add(labelSeries);

				biggerIcons(chart, nSeries);
				biggerIcons(chart, cSeries);

				for (int j = 0; j < seq.Length; j++)
				{
					var res = seq[j];
					CustomLabel label = new CustomLabel(startOffset, endOffset, new string(new char[] { res }), 0, LabelMarkStyle.None);
					a.AxisX.CustomLabels.Add(label);
					startOffset++;
					endOffset++;
				}
			}
			catch (Exception err)
			{
				chart.ChartAreas.Clear();
				chart.Series.Clear();
				Log.Logger.Log(NLog.LogLevel.Trace, $"Could not draw multichart support chart. Error: {err.Message}");
			}

			void biggerIcons(Chart chart1, Series series)
			{
				LegendItem legendItem = new LegendItem();
				LegendCell cell1 = new LegendCell
				{
					Name = "cell1",
					Text = series.LegendText
				};
				// here you can specify alignment, color, ..., too
				LegendCell cell2 = new LegendCell
				{
					Name = "cell2",
					CellType = LegendCellType.SeriesSymbol,
					SeriesSymbolSize = new Size(400, 100),
					Margins = new Margins(30, 30, 30, 30)
				};
				cell1.Margins = new Margins(30, 30, 30, 30);
				legendItem.Color = series.Color;
				legendItem.BackGradientStyle = series.BackGradientStyle;
				legendItem.BackSecondaryColor = series.BackSecondaryColor;
				legendItem.BorderColor = series.BorderColor;
				legendItem.Cells.Add(cell1);
				legendItem.Cells.Add(cell2);
				legendItem.SeriesName = series.Name;
				legendItem.ToolTip = series.LegendToolTip;
				chart1.Legends[LegendName].CustomItems.Add(legendItem);
			}
		}

		private void DrawConsensusChart(List<(double number, List<(char residue, int count)>)> consensus, double[] numbering, string sequence, Chart chart)
		{
			chart.ChartAreas.Clear();
			chart.Series.Clear();
			chart.Legends.Clear();


			try
			{
				chart.Legends.Add(new Legend(LegendName));
				chart.Legends[LegendName].Enabled = false;
				ChartArea a = new ChartArea();
				var startOffset = -0.5;
				var endOffset = 0.5;
				//a.AxisX.Minimum = -1;
				a.AxisX.LabelStyle.Angle = -45;
				a.AxisX.IsLabelAutoFit = false;
				chart.Legends[LegendName].DockedToChartArea = "NotSet";
				//a.AxisY.Minimum = 0;
				//a.AxisY.Maximum = 1;
				chart.ChartAreas.Add(a);

				var nums = new (double number, List<(char residue, int count)>)[numbering.Length];
				var xs = Enumerable.Range(0, numbering.Length).ToArray();
				int i = 0;
				foreach (var num in numbering)
				{
					var ConsensusAtNum = consensus.Where(x => x.number.IsBetweenInclusive(num - 0.01, num + 0.01));
					if (ConsensusAtNum.Any())
					{
						nums[i] = ConsensusAtNum.First();
					}
					else
					{
						nums[i] = (num, new List<(char residue, int count)>());
					}
					i++;
				}


				i = 0;
				foreach (var residue in AminoAcid.AllAminoAcidLetters)
				{
					Color color = aaColorScheme[i];

					i++;
					int[] ys = nums.Select(x => x.Item2.Where(y => y.residue == residue).FirstOrDefault().count).ToArray();
					var lab = new string(new char[] { residue });
					chart.Series.Add(new Series(i + lab));
					chart.Series.Last().IsVisibleInLegend = true;
					chart.Series.Last().Legend = LegendName;
					chart.Series[i + lab].ChartType = SeriesChartType.StackedColumn;
					chart.Series[i + lab].Color = color;
					chart.Series[i + lab].Points.DataBindXY(xs, ys);
					int j = 0;
					foreach (DataPoint dp in chart.Series[i + lab].Points)
					{
						var res = sequence[j];
						dp.ToolTip = lab + " " + "#VALY";
						if (res != residue)
						{
							dp.BackSecondaryColor = Color.White;
							dp.Color = Color.FromArgb(100, dp.Color);
							//dp.BackHatchStyle = ChartHatchStyle.LightUpwardDiagonal;
						}
						j++;
					}
				}
				for (int j = 0; j < numbering.Length; j++)
				{
					var res = sequence[j];
					var num = numbering[j];
					CustomLabel label = new CustomLabel(startOffset, endOffset, new string(new char[] { res }), 0, LabelMarkStyle.None);
					a.AxisX.CustomLabels.Add(label);
					startOffset++;
					endOffset++;
				}
			}
			catch (Exception err)
			{
				chart.ChartAreas.Clear();
				chart.Series.Clear();
				Log.Logger.Log(NLog.LogLevel.Trace, $"Could not draw germline support support chart. Error: {err.Message}");
			}
		}

		private void DrawTemplateSupportChart(RegionType curRegion)
		{

			List<(double number, List<(char residue, int count)>)> consensus = ReadFilter.GetConsensusFromReads(Document.NumberedTemplates);
			string sequence;
			double[] numbering;
			if (SelectedContigs.TryGetValue(curRegion, out RankedContig middle))
			{
				numbering = middle.numbering;
				sequence = middle.contig.Sequence;
			}
			else
			{
				GetConsensusContig(curRegion, out numbering, out sequence);
			}


			var chart = GetContigTemplateChart(curRegion);
			DrawConsensusChart(consensus, numbering, sequence, chart);
		}

		private void GetConsensusContig(RegionType curRegion, out double[] numbering, out string sequence)
		{
			var borders = Helpers.GetBorders(curRegion);
			var floor = borders.Item1;
			var ceiling = borders.Item2;

			var seqDict = Document.Consensus.Where(x => x.number.IsBetweenInclusive(floor, ceiling)).ToDictionary(x => x.number, x => x.Item2.OrderByDescending(z => z.count).Select(y => y.residue).First());
			numbering = Helpers.OrderImgtNumbering(Document.Consensus.Where(x => x.number.IsBetweenInclusive(floor, ceiling)).Select(x => x.number));
			sequence = new string(numbering.Select(x => seqDict[x]).ToArray());
		}

		private void DrawConservationSupportChart(RegionType curRegion)
		{
			string csvPath = Helpers.GetProbabilityDistribution(Document.Locus);

			var chart = GetContigConservationChart(curRegion);



			List<(double number, List<(char residue, int count)>)> consensus = ReadFilter.ReadProbabilityDistributionFromCsv(csvPath);
			
			string sequence;
			double[] numbering;
			if (SelectedContigs.TryGetValue(curRegion, out RankedContig middle))
			{
				numbering = middle.numbering;
				sequence = middle.contig.Sequence;
			}
			else
			{
				GetConsensusContig(curRegion, out numbering, out sequence);
			}


			DrawConsensusChart(consensus, numbering, sequence, chart);
		}

		private Chart GetContigPeaksChart(RegionType curRegion)
		{
			return contigCharts[curRegion][0];
		}

		private Chart GetContigTemplateChart(RegionType curRegion)
		{
			return contigCharts[curRegion][1];
		}

		private Chart GetContigSpectrumChart(RegionType curRegion)
		{
			return contigCharts[curRegion][2];
		}

		private Chart GetContigConservationChart(RegionType curRegion)
		{
			return contigCharts[curRegion][3];
		}

		private Chart GetContigMultiChart(RegionType curRegion)
		{
			return contigCharts[curRegion][4];
		}

		private void SetBestTemplate()
		{
			if (Document.Locus == LociEnum.TBD)
				Document.Locus = Helpers.GetLocusEnum(Document.Templates.Last().Name);

			LociEnum chain = Document.Locus;

			var seq = "";
			var names = "";
			List<double> numbering = new List<double>();
			List<double> nameNumbering = new List<double>();

			foreach (var geneRegion in Enum.GetValues(typeof(ImgtFilterLib.Enums.RegionEnum)))
			{
				ImgtFilterLib.Enums.RegionEnum curRegion = (ImgtFilterLib.Enums.RegionEnum)geneRegion;
				switch (curRegion)
				{
					case ImgtFilterLib.Enums.RegionEnum.VRegion:
						break;
					case ImgtFilterLib.Enums.RegionEnum.DRegion:
						break;
					case ImgtFilterLib.Enums.RegionEnum.JRegion:
						break;
					case ImgtFilterLib.Enums.RegionEnum.CRegion:
						break;
					case ImgtFilterLib.Enums.RegionEnum.CH1:
						continue;
					case ImgtFilterLib.Enums.RegionEnum.H:
						continue;
					case ImgtFilterLib.Enums.RegionEnum.CH2:
						continue;
					case ImgtFilterLib.Enums.RegionEnum.CH3_CHS:
						continue;
					case ImgtFilterLib.Enums.RegionEnum.Invalid:
						continue;
					default:
						continue;
				}
				var options = GetRegionDb(curRegion);

				if (!options.Any())
					continue;

				var innerSeq = Helpers.GetSequence(Document.CurrentPrediction.Sequence, Document.CurrentPredictionNumbering, curRegion, chain, out double[] nums);

				if (innerSeq == "")
				{
					innerSeq = Helpers.GetSequence(Document.NumberedTemplates.First().read.Sequence, Document.NumberedTemplates.First().numbering, curRegion, chain, out nums);
				}

				var bestMatch = PsrmLib.Scoring.Alignment.FindBestMatch(innerSeq, options.Select(x => x.Item2), alignerConstructor: () =>
				{
					var a = PsrmLib.Settings.DefaultTemplateAligner;
					a.GapOpenCost = -80;
					a.GapExtensionCost = -80;
					return a;
				});
				var matchedSeq = bestMatch.SecondSequence.ConvertToString();
				var matchedName = options.Where(x => x.Item2.Contains(matchedSeq.Replace("-", ""))).First().Item1;

				seq += matchedSeq;
				names += matchedName + "_";
				numbering.AddRange(nums.Take(matchedSeq.Length));
				nameNumbering.AddRange(nums.Take(matchedName.Length + 1));
			}

			Document.CurrentBestTemplate = new Peptide(seq, names);
			Document.CurrentBestTemplateNumbering = numbering.ToArray();
			Document.CurrentNameNumbering = nameNumbering.ToArray();

			(string sequence, double[] numbering) p = ReadFilter.GetAlternativeConsensusSequence(Document.Consensus, (new Peptide(Document.CurrentBestTemplate), Document.CurrentBestTemplateNumbering));
			Document.CurrentConsensus = new Peptide(p.sequence, "Read Consensus");
			Document.CurrentConsensusNumbering = p.numbering;
		}

		private void Olv_ItemCheck(object sender, ItemCheckEventArgs e)
		{
			var olv = ((ObjectListView)sender);

			olv.ItemCheck -= Olv_ItemCheck;
			olv.ItemChecked -= Olv_ItemChecked;

			((ObjectListView)sender).UncheckAll();

			olv.ItemCheck += Olv_ItemCheck;
			olv.ItemChecked += Olv_ItemChecked;
		}

		private void Olv_ItemCheckPredictions(object sender, ItemCheckEventArgs e)
		{
			var olv = ((ObjectListView)sender);

			olv.ItemCheck -= Olv_ItemCheckPredictions;

			((ObjectListView)sender).UncheckAll();

			olv.ItemCheck += Olv_ItemCheckPredictions;
		}

		private void refreshToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Reload();
		}

		private void fromSavedResultsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Document = Document.LoadProcessed();
			propertyGrid.SelectedObject = Settings;
			propertyGrid.Refresh();
			if (Document == null)
				return;

			InitializeFrContigAndScoreViews();
			InitializeCdrContigAndScoreViews();

			if (Document != null && propertyGrid.SelectedObject == null)
			{
				propertyGrid.SelectedObject = Settings;
			}

			Reload();
		}

		private void fromInputClassToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var ofd = new OpenFileDialog();
			if (ofd.ShowDialog() == DialogResult.OK)
			{
				Document = Document.LoadFromInputClassPath(ofd.FileName, Settings);
			}

			if (Document == null)
				return;
			StartUp();
		}

		private void StartUp()
		{
			InitializeFrContigAndScoreViews();
			InitializeCdrContigAndScoreViews();

			if (propertyGrid.SelectedObject == null)
			{
				propertyGrid.SelectedObject = Settings;
			}

			Reload();

			foreach (var region in Helpers.GetAllRegionEnums())
			{
				RegionType curRegion = (RegionType)region;
				if (curRegion == RegionType.None)
					continue;

				DrawMultiSupportChart(curRegion);
				DrawPeaksSupportChart(curRegion);
				DrawTemplateSupportChart(curRegion);
				DrawSpectrumSupportChart(curRegion);
				DrawConservationSupportChart(curRegion);
			}
		}

		private void fromPathsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Document = Document.LoadFromPaths(Document == null ? new Document.Settings() : Settings);
			if (Document == null)
				return;

			StartUp();
		}

		private void saveInputAsGenericFilesToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Document.SaveInput();
		}

		#endregion

		#region ControlGetters

		private (string, string)[] GetRegionDb(ImgtFilterLib.Enums.RegionEnum region)
		{
			EntryDbPath = /*EntryDbPath ?? */PsrmLib.API.ImgtAssembler.ConstructEntryDb();
			Dictionary<string, string> entries = FastaIO.ReadFasta(EntryDbPath);

			var chainName = "";

			switch (Document.Locus)
			{
				case LociEnum.Kappa:
					chainName = "K";
					break;
				case LociEnum.Lambda:
					chainName = "L";
					break;
				case LociEnum.Heavy:
					chainName = "H";
					break;
				case LociEnum.TBD:
					break;
				default:
					break;
			}

			IEnumerable<KeyValuePair<string, string>> toReturn = null;

			switch (region)
			{
				case ImgtFilterLib.Enums.RegionEnum.VRegion:
					toReturn = entries.Where(x => Regex.IsMatch(x.Key, $@"IG{chainName}V"));
					break;
				case ImgtFilterLib.Enums.RegionEnum.DRegion:
					toReturn = entries.Where(x => Regex.IsMatch(x.Key, $@"IG{chainName}D"));
					break;
				case ImgtFilterLib.Enums.RegionEnum.JRegion:
					toReturn = entries.Where(x => Regex.IsMatch(x.Key, $@"IG{chainName}J"));
					break;
				case ImgtFilterLib.Enums.RegionEnum.CRegion:
					toReturn = entries.Where(x => Regex.IsMatch(x.Key, $@"IG{chainName}[CG]1*"));
					break;
				case ImgtFilterLib.Enums.RegionEnum.CH1:
					break;
				case ImgtFilterLib.Enums.RegionEnum.H:
					break;
				case ImgtFilterLib.Enums.RegionEnum.CH2:
					break;
				case ImgtFilterLib.Enums.RegionEnum.CH3_CHS:
					break;
				case ImgtFilterLib.Enums.RegionEnum.Invalid:
					break;
				default:
					break;
			}

			return toReturn.Select(x => (Regex.Replace(Regex.Replace(x.Key, ".*_", ""), @"_.*", ""), x.Value)).ToArray();
		}

		private SplitContainer GetScoreSplit(RegionType region)
		{
			if (!ScoreSplits.TryGetValue(region, out SplitContainer curSplit))
			{
				var curTab = new TabPage(Enum.GetName(typeof(RegionType), region));
				scoreTabControl.TabPages.Add(curTab);
				curSplit = new SplitContainer
				{
					Parent = curTab,
					Dock = DockStyle.Fill
				};
				ScoreSplits.Add(region, curSplit);
				curSplit.FixedPanel = FixedPanel.Panel2;

				// charts
				Chart c = new Chart
				{
					Dock = DockStyle.Fill,
					Parent = curSplit.Panel2
				};
				scoreCharts.Add(region, c);
				ControlsWithDockedCharts.Add(curSplit.Panel2, c);
				c.MouseDoubleClick += C_MouseDoubleClick;
			}

			return curSplit;
		}

		private void C_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			var chart = (Chart)sender;
			Form newWindow = new Form();

			Chart clonedChart = CloneChart(chart);
			clonedChart.Parent = newWindow;
			clonedChart.Dock = DockStyle.Fill;

			clonedChart.Legends[LegendName].Enabled = true;

			// spectrum chart
			if (clonedChart.Series.First().ChartType == SeriesChartType.Column)
			{
				foreach (var series in clonedChart.Series)
				{
					foreach (var dp in series.Points)
					{
						dp.Label = dp.ToolTip;
					}
				}
			}

			newWindow.Controls.Add(clonedChart);

			newWindow.Show();
		}

		private ObjectListView GetScoreOlv(RegionType region)
		{
			if (!scoreViews.TryGetValue(region, out ObjectListView scoreOlv))
			{
				var curSplit = GetScoreSplit(region);

				scoreOlv = new ObjectListView()
				{
					ShowGroups = false,
					AutoArrange = false,
					FilterMenuBuildStrategy = new CustomFilterMenuBuilder(),
					ShowItemToolTips = true,
					ShowFilterMenuOnRightClick = true,
					ShowSortIndicators = true,
					FullRowSelect = true,
					Dock = DockStyle.Fill,
					Font = templateObjectListView.Font,
					UseFiltering = true,
					Parent = curSplit.Panel1,
					ContextMenu = GetContigContextMenu(),
				};

				scoreOlv.CellRightClick += ContigOlvCellRightClick;
				scoreViews.Add(region, scoreOlv);
				scoreOlv.FormatRow += ScoreOlv_FormatRow;
				scoreOlv.Tag = region;
			}

			return scoreOlv;
		}

		private void ContigOlvCellRightClick(object sender, CellRightClickEventArgs e)
		{
			try
			{
				var olv = ((ObjectListView)sender);
				if (e.Item != null)
				{
					olv.ContextMenu.Tag = e.Item.RowObject;
				}
			}
			catch (Exception er)
			{

				MessageBox.Show($"an error occured: {er.Message}");
			}
		}

		private ContextMenu GetContigContextMenu()
		{
			ContextMenu icm = new ContextMenu();

			icm.MenuItems.Add("Select contig For prediction", new EventHandler(SelectContigForPrediction));
			icm.MenuItems.Add("Check contig for recombination", new EventHandler(CheckContigBox));
			icm.MenuItems.Add("Add Mutated variant of this contig", new EventHandler(AddMutatedVariant));
			//icm.MenuItems.Add("Exclude contig from analysis", new EventHandler(RemoveRead));

			icm.MenuItems.Add("Write");
			icm.MenuItems[icm.MenuItems.Count - 1].MenuItems.Add("Write all shown predictions to proforma", new EventHandler(WriteAllToProForma));
			icm.MenuItems[icm.MenuItems.Count - 1].MenuItems.Add("Write all shown predictions to csv", new EventHandler(WriteAllToCsv));

			icm.MenuItems.Add("Analyze");
			icm.MenuItems[icm.MenuItems.Count - 1].MenuItems.Add("Recombine adjacent checked candidates", new EventHandler(RecombineAdjacent));
			icm.MenuItems[icm.MenuItems.Count - 1].MenuItems.Add("Rescore all shown candidates for this region", new EventHandler(ShowMultiScoreForAll));
			icm.MenuItems[icm.MenuItems.Count - 1].MenuItems.Add("Refine (I/L) all shown predictions", new EventHandler(RefineAll));

			icm.MenuItems.Add("View");
			icm.MenuItems[icm.MenuItems.Count - 1].MenuItems.Add("Show Annotated spectrum", new EventHandler(ShowSpectrumViewContig));
			//icm.MenuItems[icm.MenuItems.Count - 1].MenuItems.Add("Show Read Coverage", new EventHandler(ShowReadCoverageViewContig));

			return icm;
		}

		private void WriteAllToCsv(object sender, EventArgs e)
		{
			var cm = (MenuItem)sender;
			var olv = (ObjectListView)cm.GetContextMenu().SourceControl;
			var olvExporter = new OLVExporter(olv,
			olv.FilteredObjects);
			string csv = olvExporter.ExportTo(OLVExporter.ExportFormat.CSV);


			var sfd = new SaveFileDialog()
			{
				AddExtension = true,
				DefaultExt = ".csv",
				Filter = "csv files (*.csv)|*.csv",
			};

			if (sfd.ShowDialog() == DialogResult.OK)
			{
				using (StreamWriter sw = new StreamWriter(sfd.FileName))
				{
					sw.Write(csv);
				}
			}
		}

		private void WriteAllToProForma(object sender, EventArgs e)
		{
			try
			{
				var cm = (MenuItem)sender;
				var olv = (ObjectListView)cm.GetContextMenu().SourceControl;
				var item = (RankedContig)cm.GetContextMenu().Tag;

				var sfd = new SaveFileDialog()
				{
					AddExtension = true,
					DefaultExt = ".proforma",
				};

				var contigs = olv.FilteredObjects.Cast<RankedContig>().ToArray();

				if (sfd.ShowDialog() == DialogResult.OK)
				{
					try
					{
						using (StreamWriter file =
							new StreamWriter(sfd.FileName))
						{
							string name = Path.GetFileNameWithoutExtension(sfd.FileName);
							int i = 0;

							foreach (var contig in contigs)
							{
								file.WriteLine($">{name}_{i}");

								StringBuilder builder = new StringBuilder();
								var nterm = contig.contig.Nterm == null ? 0 : contig.contig.Nterm.Delta;
								var cterm = contig.contig.Cterm == null ? 0 : contig.contig.Cterm.Delta;
								builder.Append($"[{nterm}]_");
								builder.Append($"{contig.contig.Sequence}");
								builder.Append($"_[{cterm}]");
								file.WriteLine(builder);
								i++;
							}
						}
					}
					catch (Exception e2)
					{
						MessageBox.Show($"failed to save: {e2.Message}");
					}
				}
			}
			catch (Exception er)
			{
				MessageBox.Show($"an error occured: {er.Message}");
			}
		}

		private void SelectContigForPrediction(object sender, EventArgs e)
		{
			try
			{
				var cm = (MenuItem)sender;
				var olv = (ObjectListView)cm.GetContextMenu().SourceControl;
				var item = (RankedContig)cm.GetContextMenu().Tag;

				RegionType curRegion = (RegionType)olv.Tag;
				List<RegionType> toReprocess = new List<RegionType>();

				SetCurrentSelection(curRegion, item);

				toReprocess = new List<RegionType>()
				{
					curRegion
				};

				// if the checked column has adjacent cdr, draw
				if (curRegion == RegionType.FR1)
				{
					toReprocess.Add(RegionType.CDR1);
				}
				else if (curRegion == RegionType.FR4)
				{
					toReprocess.Add(RegionType.CDR3);
				}
				else
				{
					var (nFlankRegion, cFlankRegion) = Helpers.GetFlanking(curRegion);

					toReprocess.Add(nFlankRegion);
					toReprocess.Add(cFlankRegion);
				}

				// and repopulate the template listview to show what we did.
				SetCurrentPrediction();

				PopulateTemplateListView();

				foreach (var reg in toReprocess.Distinct())
				{
					FillContigandScoreViews(reg);

					DrawPeaksSupportChart(reg);
					DrawMultiSupportChart(reg);
					DrawTemplateSupportChart(reg);
					DrawSpectrumSupportChart(reg);
					DrawConservationSupportChart(reg);
				}
				Reload();
			}
			catch (Exception er)
			{
				MessageBox.Show($"an error occured: {er.Message}");
			}
		}

		private void CheckContigBox(object sender, EventArgs e)
		{
			try
			{
				var cm = (MenuItem)sender;
				var olv = (ObjectListView)cm.GetContextMenu().SourceControl;
				var item = (RankedContig)cm.GetContextMenu().Tag;

				RegionType curRegion = (RegionType)olv.Tag;

				var contigOlv = GetOlv(curRegion);
				contigOlv.CheckObject(item);
			}
			catch (Exception er)
			{
				MessageBox.Show($"an error occured: {er.Message}");
			}
		}

		private void AddMutatedVariant(object sender, EventArgs e)
		{
			try
			{
				var cm = (MenuItem)sender;
				var olv = (ObjectListView)cm.GetContextMenu().SourceControl;
				var item = (RankedContig)cm.GetContextMenu().Tag;

				RegionType curRegion = (RegionType)olv.Tag;
				var pep = (Peptide)item.contig.Clone();
				string oldSequence = pep.Sequence;

				string newSequence = RequestSequence(oldSequence);
				pep.Sequence = newSequence;
				pep.Modifications = pep.Modifications != null && pep.Modifications.Length == pep.Sequence.Length ? pep.Modifications : new Modification[newSequence.Length];

				Document.ClipAndRank(new Peptide[] { pep });
				UpdateSessionClippedContigs(curRegion);
				Reload();
			}
			catch (Exception er)
			{
				MessageBox.Show($"an error occured: {er.Message}");
			}
		}

		/// <summary>
		/// Get the corresponding objectlistview where the contigs are shown
		/// </summary>
		/// <param name="region"></param>
		/// <returns></returns>
		private ObjectListView GetOlv(RegionType region)
		{
			if (!contigViews.TryGetValue(region, out ObjectListView olv))
			{
				SplitContainer parent = null;
				switch (region)
				{
					case RegionType.FR1:
						parent = splitContainer8;
						break;
					case RegionType.CDR1:
						parent = splitContainer9;
						break;
					case RegionType.FR2:
						parent = splitContainer10;
						break;
					case RegionType.CDR2:
						parent = splitContainer11;
						break;
					case RegionType.FR3:
						parent = splitContainer12;
						break;
					case RegionType.CDR3:
						parent = splitContainer13;
						break;
					case RegionType.FR4:
						parent = splitContainer14;
						break;
					default:
						break;
				}

				olv = new ObjectListView()
				{
					ShowGroups = false,
					AutoArrange = false,
					FilterMenuBuildStrategy = new CustomFilterMenuBuilder(),
					ShowItemToolTips = true,
					ShowFilterMenuOnRightClick = true,
					ShowSortIndicators = true,
					FullRowSelect = true,
					CheckBoxes = true,
					UseFiltering = true,
					Parent = parent.Panel1,
					ContextMenu = GetContigContextMenu(),
					Dock = DockStyle.Fill,
					Font = templateObjectListView.Font,
				};

				olv.FormatRow += Olv_FormatRow;
				olv.CellRightClick += ContigOlvCellRightClick;

				// charts
				var tc = new TabControl
				{
					Parent = parent.Panel2,
					Dock = DockStyle.Fill
				};
				tc.TabPages.Add(new TabPage("Peaks"));
				tc.TabPages.Add(new TabPage("Template"));
				tc.TabPages.Add(new TabPage("Spectrum"));
				tc.TabPages.Add(new TabPage("Conservation"));
				tc.TabPages.Add(new TabPage("Multi"));

				List<Chart> cList = new List<Chart>(4);
				foreach (var tabPage in tc.TabPages)
				{
					Chart c = new Chart
					{
						Dock = DockStyle.Fill,
						Parent = (TabPage)tabPage
					};
					cList.Add(c);
					c.MouseDoubleClick += C_MouseDoubleClick;
					ControlsWithDockedCharts.Add((TabPage)tabPage, c);
				}
				contigCharts.Add(region, cList.ToArray());

				contigViews.Add(region, olv);
				olv.Tag = region;
			}

			return olv;
		}

		public static string RequestSequence(string sequence)
		{
			Form prompt = new Form()
			{
				Width = 500,
				Height = 150,
				FormBorderStyle = FormBorderStyle.FixedDialog,
				StartPosition = FormStartPosition.CenterScreen
			};

			TextBox tb = new TextBox()
			{
				Text = sequence,
				Multiline = true,
				Width = 400,
				AutoSize = true,
				WordWrap = true,
				Anchor = AnchorStyles.None,
			};

			prompt.Controls.Add(tb);

			Button confirmation = new Button() { Text = "Ok", Left = 350, Width = 100, Top = 70, DialogResult = DialogResult.OK };
			confirmation.Click += (sender, e) => { prompt.Close(); };
			prompt.Controls.Add(confirmation);
			prompt.AcceptButton = confirmation;

			if (prompt.ShowDialog() == DialogResult.OK)
			{
				return tb.Text;
			}

			throw new Exception("cancelled");
		}

		#endregion

		#region Helpers


		public static void AssignIsAndKs(List<(PeaksPeptideData read, double[] numbering)> numberedReads, RankedContig[] toChange, List<(Peptide template, double[] numbering)> numberedTemplates)
		{
			List<Dictionary<double, char>> templatePeps = numberedTemplates.Select(x => x.template.Sequence.Zip(x.numbering, (z, y) => (y, z)).ToDictionary(a => a.y, a => a.z)).ToList();
			// I/L and Q/K
			// template is leading
			// then peaks
			for (int i = 0; i < toChange.Count(); i++)
			{
				int idx = 0;
				var contig = toChange[i];
				var seq = (string)contig.contig.Sequence.Clone();
				var sb = new StringBuilder();
				foreach (var res in seq)
				{
					var innerRes = res;
					//if (res == 'K')
					//{
					//	IEnumerable<char> options = templatePeps.Where(x => x.ContainsKey(contig.numbering[idx])).Select(x => x[contig.numbering[idx]]).GroupBy(x => x).OrderByDescending(x => x.Count()).Select(x => x.Key);
					//	if (options.Where(x => x == 'Q' || x == 'K').Any())
					//	{
					//		innerRes = options.First();
					//	}
					//	else
					//	{

					//		string pattern = idx > 1 && idx < contig.contig.Length - 1 ? contig.contig.Sequence.Substring(idx - 1, 3) : "BBBBBBBB";
					//		var ans = numberedReads.Where(x => x.read.Peptide.Contains(pattern)).Select(x =>
					//		{
					//			var idxOfR = x.read.Peptide.IndexOf(contig.contig.Sequence.Substring(idx - 1, 3)) + 1;
					//			return x.read.OriginalSequence[idxOfR];
					//		}).GroupBy(x => x).OrderByDescending(x => x.Count());

					//		if (ans.Any())
					//		{
					//			innerRes = ans.First().Key;
					//		}
					//		else { }
					//	}
					//}
					//else
					if (res == 'L' || res == 'I')
					{
						IEnumerable<char> options = templatePeps.Where(x => x.ContainsKey(contig.numbering[idx])).Select(x => x[contig.numbering[idx]]).GroupBy(x => x).OrderByDescending(x => x.Count()).Where(x => x.Key == 'I' || x.Key == 'L').Select(x => x.Key);
						if (options.Where(x => x == 'I' || x == 'L').Any())
						{
							innerRes = options.First();
						}
					}
					sb.Append(innerRes);
					idx++;
				}
				if (toChange[i].contig.Sequence != sb.ToString())
				{
					toChange[i].contig.Sequence = sb.ToString();
				}
			}
		}

		private ObjectListView ShowPredictionsView(RankedContig[] predictions = null, int[] range = null, string title = "Predictions")
		{
			if (Document.Predictions == null && predictions == null)
			{
				MessageBox.Show("No predictions yet");
				return null;
			}

			var tabPage = MakeClosableTab(title, mainTabControl);

			var split = new SplitContainer
			{
				SplitterDistance = 700,
				Parent = tabPage,
				Dock = DockStyle.Fill
			};
			// create the olv and columns

			ObjectListView predictionOlv = new ObjectListView()
			{
				UseFiltering = true,
				FilterMenuBuildStrategy = new CustomFilterMenuBuilder(),
				ShowItemToolTips = true,
				ShowFilterMenuOnRightClick = true,
				ShowSortIndicators = true,
				ShowGroups = false,
				CheckBoxes = false,
				Parent = split.Panel1,
				Dock = DockStyle.Fill,
				Font = templateObjectListView.Font,
				FullRowSelect = true,
			};

			predictions = predictions ?? Document.Predictions;

			IEnumerable<object> objectsToBe = predictions.Select(x => (object)x);
			AddColumns(predictions, predictionOlv);
			Helpers.CreateColumn(predictionOlv, "massDelta", HorizontalAlignment.Left, delegate (Object obj)
			{
				var parsed = (RankedContig)obj;
				if (parsed.contig.Cterm == null)
					return "";
				else
				{
					var monoMass = Proteomics.ConvertAverageMassToMonoIsotopic(Document.Spectrum.PrecursorMass, true);
					return Math.Round(monoMass - (parsed.contig.MonoIsotopicMass - parsed.contig.Nterm.Delta - parsed.contig.Cterm.Delta), 3);
				}
			}, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			predictionOlv.AllColumns.Last().IsVisible = false;
			Helpers.CreateColumn(predictionOlv, "mass Delta No Termini", HorizontalAlignment.Left, delegate (Object obj)
			{
				var parsed = (RankedContig)obj;
				if (parsed.contig.Cterm == null)
					return "";
				else
				{
					var monoMass = Proteomics.ConvertAverageMassToMonoIsotopic(Document.Spectrum.PrecursorMass, true);
					return Math.Round(monoMass - (parsed.contig.MonoIsotopicMass), 3);
				}
			}, customFilterMenuBuilder: new RangeFilterMenuBuilder(), objectsToBe: objectsToBe);
			predictionOlv.AllColumns.Last().IsVisible = true;
			// we set the width to either the width of the manestring, or the width of the widest value.

			predictionOlv.SelectedIndexChanged += PredictionOlv_SelectedIndexChanged;

			predictionOlv.SetObjects(predictions);

			predictionOlv.RebuildColumns();

			predictionOlv.ContextMenu = GetContigContextMenu();

			predictionOlv.CellRightClick += ContigOlvCellRightClick;

			// charts
			var tc = new TabControl
			{
				Parent = split.Panel2,
				Dock = DockStyle.Fill
			};
			tc.TabPages.Add(new TabPage("BU vs MD"));
			tc.TabPages.Add(new TabPage("Multi"));
			tc.TabPages.Add(new TabPage("Scatter"));

			predictionOlv.Tag = (range[0], range[1], tc);

			foreach (var page in tc.TabPages)
			{
				Chart c = new Chart
				{
					Parent = (TabPage)page,
					Dock = DockStyle.Fill
				};
				c.MouseDoubleClick += C_MouseDoubleClick;
				ControlsWithDockedCharts.Add((TabPage)page, c);
			}
			
			predictionOlv.AfterSorting += PredictionOlv_AfterSorting;
			predictionOlv.ItemsChanged += PredictionOlv_ItemsChanged;

			DrawPredictionDotPlot(predictionOlv);
			DrawPrediction2DPlot(predictionOlv);

			return predictionOlv;
		}

		private void ShowRefinedPredictionsView(RankedContig[] backup = null, int[] range= null)
		{
			if (backup == null)
			{
				throw new Exception("undefined: predictions should be provided when refining");
			}

			var predictions = new RankedContig[backup.Length];

			backup.CopyTo(predictions, 0);
			predictions = predictions.Select(x =>
			{
				x.contig = new Peptide(x.contig);
				return x;
			}).ToArray();
			AssignIsAndKs(Document.NumberedReads.Select(x =>
			{
				var newRead = new PeaksPeptideData(x.read);
				newRead.Peptide = newRead.OriginalSequence;
				return (x.read, x.numbering);
			}).ToList(), predictions, Document.NumberedTemplates);
			for (int i = 0; i < predictions.Length; i++)
			{
				predictions[i].contig = predictions[i].contig.ShiftPeptideToOptimum(Document.Spectrum, Document.ScoringModel);
			}

			predictions = Helpers.RankFullLength(predictions.Select(x => (x.contig, x.numbering)).ToArray(), SequenceSource.Contig, Document, Document.Locus, range);

			ShowPredictionsView(predictions, range, "Refined predictions");
		}

		private void PredictionOlv_SelectedIndexChanged(object sender, EventArgs e)
		{
			ObjectListView olv = ((ObjectListView)sender);
			RankedContig item = new RankedContig();
			var multiScoreRange = ((int, int, TabControl))olv.Tag;
			var chart = ControlsWithDockedCharts[multiScoreRange.Item3.TabPages[1]];
			try
			{
				//if (olv.FocusedObject != null)
				object selected = olv.SelectedObject;
				if (selected != null)
				{
					item = (RankedContig)selected;
				}
				else
				{
					return;
				}
			}
			catch (Exception e2)
			{
				Log.Logger.Log(NLog.LogLevel.Trace, $"Could convert the selected object to a contig. Error: {e2.Message}");
				return;
			}

			var length = multiScoreRange.Item2 - multiScoreRange.Item1;

			(double min, double max)[] imgt = olv.FilteredObjects.Cast<RankedContig>().Select(x =>
			{
				var nums = x.numbering;
				if (length <= x.contig.Sequence.Length)
					nums = x.numbering.Skip(multiScoreRange.Item1).Take(length).ToArray();
				return (nums.Min(), nums.Max());
			}).ToArray();

			// highest min and lowest max: guarantee all entries have the same endingres
			var minImgt = imgt.Select(x => x.min).Max();
			var maxImgt = imgt.Select(x => x.max).Min();

			var seq = item.contig.Sequence;
			var numbers = item.numbering;
			int minIdx = numbers.IndexOf(minImgt);
			int maxIdx = numbers.IndexOf(maxImgt);
			int innerLength = maxIdx - minIdx + 1;

			if (length <= item.contig.Sequence.Length)
			{
				seq = item.contig.Sequence.Substring(minIdx, innerLength);
				numbers = item.numbering.Skip(minIdx).Take(innerLength).ToArray();
			}
			try
			{
				DrawMultiChart(MultiScores[seq.Replace('I', 'L')], numbers, seq, chart);
			}
			catch (Exception)
			{
			}
		}

		private void PredictionOlv_ItemsChanged(object sender, ItemsChangedEventArgs e)
		{
			DrawPredictionDotPlot(sender);
		}

		private void PredictionOlv_AfterSorting(object sender, AfterSortingEventArgs e)
		{
			DrawPredictionDotPlot(sender);
		}

		private void DrawPredictionDotPlot(object sender)
		{
			ObjectListView olv = ((ObjectListView)sender);
			var chart = ControlsWithDockedCharts[(((int, int, TabControl))olv.Tag).Item3.TabPages[2]];
			var ordered = new List<(int, RankedContig)>(olv.GetItemCount());

			for (int i = 0; i < olv.GetItemCount(); i++)
			{
				int index = olv.GetDisplayOrderOfItemIndex(i);
				var obj = (RankedContig)olv.GetItem(i).RowObject;
				ordered.Add((index, obj));
			}

			var visiblePredictions = ordered.OrderBy(x => x.Item1).Select(x => x.Item2).ToArray();
			DrawDotPlotForContigs(visiblePredictions, chart, true);
		}

		private void DrawPrediction2DPlot(object sender)
		{
			ObjectListView olv = ((ObjectListView)sender);
			var chart = ControlsWithDockedCharts[(((int, int, TabControl))olv.Tag).Item3.TabPages[0]];
			var ordered = new List<(int, RankedContig)>(olv.GetItemCount());

			for (int i = 0; i < olv.GetItemCount(); i++)
			{
				int index = olv.GetDisplayOrderOfItemIndex(i);
				var obj = (RankedContig)olv.GetItem(i).RowObject;
				ordered.Add((index, obj));
			}

			var visiblePredictions = ordered.OrderBy(x => x.Item1).Select(x => x.Item2).ToArray();
			Draw2DPlot(visiblePredictions, chart);
		}

		/// <summary>
		/// Convert a template or numbered read to a gapped string, used to unify all lengths
		/// </summary>
		/// <param name="parsed"></param>
		/// <param name="innerNumbers"></param>
		/// <returns></returns>
		private char[] ConvertToGappedString((string template, double[] numbering) parsed, RegionType region, bool overrideIt = false)
		{
			if (!overrideIt && GappedSequences.TryGetValue((parsed, region), out char[] gapped))
			{
				return gapped;
			}

			var contigs = GetSessionClippedContigs(region);
			var numInRange = Helpers.OrderImgtNumbering(contigs.SelectMany(x => x.numbering).ToArray());

			gapped = ConvertToGappedString(parsed, numInRange);

			if (!overrideIt)
			{
				if (GappedSequences.TryGetValue((parsed, region), out _))
				{
					GappedSequences[(parsed, region)] = gapped;
				}
				else
				{
					GappedSequences.Add((parsed, region), gapped);
				}
			}
			return gapped;
		}

		/// <summary>
		/// Convert a template or numbered read to a gapped string, used to unify all lengths
		/// </summary>
		/// <param name="parsed"></param>
		/// <param name="innerNumbers"></param>
		/// <returns></returns>
		private char[] ConvertToGappedString((string template, double[] numbering) parsed, double[] numInRange)
		{
			var count = numInRange.Count();

			var zipped = parsed.numbering.Zip(parsed.template, (num, res) => (num, res));

			var charArray = new char[count];

			for (int i = 0; i < count; i++)
			{
				var matched = zipped.Where(x => x.num.IsBetweenInclusive(numInRange[i] - 0.01, numInRange[i] + 0.01));
				if (matched.Any())
				{
					charArray[i] = matched.First().res;
				}
				else
				{
					charArray[i] = '-';
				}
			}

			return charArray;
		}

#endregion

		public Control Clone<T>(T controlToClone)
			where T : Control
		{
			// what we want to return
			Control toReturn = new Control();

			T instance = Activator.CreateInstance<T>();
			PropertyInfo[] controlPropertiesGeneric = typeof(T).GetProperties();

			foreach (PropertyInfo propInfo in controlPropertiesGeneric)
			{
				if (propInfo.CanWrite)
				{
					if (propInfo.Name != "WindowTarget" && propInfo.Name != "Parent")
						propInfo.SetValue(toReturn, propInfo.GetValue(controlToClone, null), null);
				}
			}

			toReturn = instance;

			foreach (Control nested in controlToClone.Controls)
			{
				if (nested.GetType() != typeof(Chart))
				{
					var nestedCopy = Clone(nested);
					nestedCopy.Parent = instance;
					nestedCopy.Dock = nested.Dock;
					toReturn.Controls.Add(nestedCopy);
				}
				else
				{
					if (ControlsWithDockedCharts.TryGetValue(controlToClone, out Chart val))
					{
						Chart clonedChart = CloneChart(val);
						clonedChart.Parent = instance;
						clonedChart.Dock = val.Dock;
						toReturn.Controls.Add(clonedChart);
					}
				}
			}

			return toReturn;
		}

		public Chart CloneChart(Chart chart)
		{
			MemoryStream stream = new MemoryStream();
			Chart clonedChart = chart;
			clonedChart.Serializer.Save(stream);
			clonedChart = new Chart();
			clonedChart.Serializer.Load(stream);
			return clonedChart;
		}



		private void outputAllCsvsToolStripMenuItem_Click(object sender, EventArgs e)
		{
			var ofd = new FolderBrowserDialog();
			if (ofd.ShowDialog() != DialogResult.OK)
				return;

			var folder = ofd.SelectedPath;

			var frs = new RegionType[] { RegionType.FR1, RegionType.FR2, RegionType.FR3, RegionType.FR4 };
			var cdrs = new RegionType[] { RegionType.CDR1, RegionType.CDR2, RegionType.CDR3 };

			DialogResult dr = MessageBox.Show("Output all displayed framework region scores to fr1-4.csv (bottom panel)?",
					  "", MessageBoxButtons.YesNo);
			if (dr == DialogResult.Yes)
			{
				foreach (var curRegion in frs)
				{
					var olv = GetScoreOlv(curRegion);
					string csv = string.Empty;
					var olvExporter = new OLVExporter(olv,
					olv.FilteredObjects);
					csv = olvExporter.ExportTo(OLVExporter.ExportFormat.CSV);

					using (StreamWriter sw = new StreamWriter(folder + curRegion.ToString() + ".csv"))
					{
						sw.Write(csv);
					}
				}
			}
			dr = MessageBox.Show("Recombine checked FRs into FR-CDR-FRs for CDR1-3 and output CDR scores to cdr1-3.csv?",
					  "", MessageBoxButtons.YesNo);
			if (dr == DialogResult.Yes)
			{
				foreach (var curRegion in cdrs)
				{
					var olv = RecombineAdjacentInner(curRegion);

					string csv = string.Empty;
					var olvExporter = new OLVExporter(olv,
					olv.FilteredObjects);
					csv = olvExporter.ExportTo(OLVExporter.ExportFormat.CSV);

					using (StreamWriter sw = new StreamWriter(folder + curRegion.ToString() + ".csv"))
					{
						sw.Write(csv);
					}
				}
			}

			dr = MessageBox.Show("Recombine checked FRs into chain candidates and output scores to chains.csv?",
					  "", MessageBoxButtons.YesNo);

			if (dr == DialogResult.Yes)
			{
				var olv = recombineAll();
				var olvExporter = new OLVExporter(olv,
				olv.FilteredObjects);
				string csv = olvExporter.ExportTo(OLVExporter.ExportFormat.CSV);

				using (StreamWriter sw = new StreamWriter(folder + "chains.csv"))
				{
					sw.Write(csv);
				}
			}

			Process.Start(folder.ToString());
		}
	}
}
