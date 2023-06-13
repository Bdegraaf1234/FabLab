using HeckLib;
using HeckLib.chemistry;
using HeckLib.ConvenienceInterfaces.SpectrumMatch;
using HeckLib.io.fileformats;
using HeckLib.io.json;
using HeckLib.masspec;
using HeckLib.utils;
using HeckLib.visualization.propgrid;
using HeckLibRawFileMgf;
using ImgtFilterLib;
using PsrmLib;
using PsrmLib.IO;
using PsrmLib.Models;
using PsrmLib.Processing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Drawing.Design;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using Wexman.Design;

namespace FabLab
{
	public class Document
    {
        #region constants
        
        private readonly static int peaks_key = 1;
        private readonly static int template_key = 2;
        private readonly static int spectrum_key = 3;
        private readonly static int conservedness_key = 4;
        private readonly static int multi_key = 5;

        #endregion

        #region input

        /// <summary>
        /// ReadFilter, which may be later swapped out for a set of reads
        /// </summary>
        public ReadFilter ReadFilter;

        /// <summary>
        /// full length contigs, Finalized in terms of Deamidation, K/Q, I/L, Pswitch,
        /// </summary>
        private List<(Peptide Match, List<ExtenderBase<Peptide>.MetaData<int>>)> FullContigs;

        public LociEnum Locus = LociEnum.TBD;

        /// <summary>
        /// temnplate sequences with numbering, provided for now
        /// </summary>
        public List<(Peptide read, double[] numbering)> NumberedTemplates;

        /// <summary>
        /// temnplate sequences with numbering, provided for now
        /// </summary>
        public List<(PeaksPeptideData read, double[] numbering)> NumberedReads;

        public Dictionary<string, List<List<Coverage>>> MultiScores = new Dictionary<string, List<List<Coverage>>>();

        /// <summary>
        /// Top down spectrum
        /// </summary>
        public SpectrumContainer Spectrum;

        public Settings CurrentSettings;

        public string Title = "FabLab";

        #endregion

        #region read only

        /// <summary>
        /// template sequences, provided for now
        /// </summary>
        public Peptide[] Templates { get => NumberedTemplates.Select(x => x.read).ToArray(); }

        /// <summary>
        /// Grabs a fresh new fragmentmodel. ETD, so CYZ, neutral losses included without off-by-one
        /// </summary>
        public PeptideFragment.FragmentModel AnnotationModel
        {
            get => new PeptideFragment.FragmentModel(PsrmFragmentModel.ModelEtd)
            {
                tolerance = new Tolerance(CurrentSettings.Tolerance, Tolerance.ErrorUnit.PPM)
            };
        }

        /// <summary>
        /// Grabs a fresh new fragmentmodel. ETD without Y, so CZ, no mass shifts. Good for shifting as we do not get problematic off by or neutral loss errors to include an extra peak, and do not weigh c-terminal fragments double because tehre are twice as many theoretical frags there.
        /// </summary>
        public PeptideFragment.FragmentModel ScoringModel { get => new PeptideFragment.FragmentModel(PsrmFragmentModel.ModelEtdNoY)
        {
            tolerance = new Tolerance(CurrentSettings.Tolerance, Tolerance.ErrorUnit.PPM)
        }; }


        #endregion

        #region output

        /// <summary>
        /// predicted full length, provided for now
        /// </summary>
        public RankedContig[] Predictions;

        /// <summary>
        /// The current prediction, it can be left empty as it just gets filled with gaps then
        /// </summary>
        public Peptide CurrentPrediction = new Peptide("");

        /// <summary>
        /// numbering for the current predictiom, same as the prediction, can be empty
        /// </summary>
        public double[] CurrentPredictionNumbering = new double[0];

        /// <summary>
        /// numbering for the current best template, same as the prediction, can be empty
        /// </summary>
        public double[] CurrentBestTemplateNumbering = new double[0];

        public double[] CurrentNameNumbering = new double[0];

        /// <summary>
        /// The current best template, it can be left empty as it just gets filled with gaps then
        /// </summary>
        public Peptide CurrentBestTemplate = new Peptide("");

        /// <summary>
        /// The current best template, it can be left empty as it just gets filled with gaps then
        /// </summary>
        public Peptide CurrentConsensus = new Peptide("");

        /// <summary>
        /// numbering for the current best template, same as the prediction, can be empty
        /// </summary>
        public double[] CurrentConsensusNumbering = new double[0];

        public List<(double number, List<(char residue, int count)>)> Consensus;

        /// <summary>
        /// clipped
        /// </summary>
        public Dictionary<RegionType, RankedContig[]> ClippedContigs = new Dictionary<RegionType, RankedContig[]>();

        public Dictionary<(double, string, double, string), RankedContig[]> FillerDict = new Dictionary<(double, string, double, string), RankedContig[]>();

        #endregion

        public class Settings
        {
            // general
            [RefreshProperties(RefreshProperties.All)]
            [Category("2. Gapfilling")]
            [DisplayName("Cdr Fillers To Display")]
            [Description("The type of cdr fillers that should be displayed.")]
            [Editor(typeof(GenericDictionaryEditor<SequenceSource, bool>), typeof(UITypeEditor))]
            public Dictionary<SequenceSource, bool> SequenceSourceToDisplay { get; set; }

            [RefreshProperties(RefreshProperties.All)]
            [Category("2. Gapfilling")]
            [DisplayName("variables to order on")]
            [Description("variables per region to order on")]
            [Editor(typeof(GenericDictionaryEditor<RegionType, int[]>), typeof(UITypeEditor))]
            public Dictionary<RegionType, int[]> OrderingVars { get; set; }

            [RefreshProperties(RefreshProperties.All)]
            [Category("1. Parsing")]
            [DisplayName("Max N Delta")]
            [Description("Maximum delta between NTerm and 0")]
            public double MaxNDelta { get; set; } = 200;

            [RefreshProperties(RefreshProperties.All)]
            [Category("1. Parsing")]
            [DisplayName("Max C Delta")]
            [Description("Maximum delta between cTerm and precursormass of the spectrum")]
            public double MaxCDelta { get; set; } = 100;

            [RefreshProperties(RefreshProperties.All)]
            [Category("2. Gapfilling")]
            [DisplayName("Gap Fill Tolerancein Da")]
            [Description("maximum difference between the total size of the gapfillers and the gap")]
            public int ToleranceInDaGapFillers { get; set; } = 5;

            [RefreshProperties(RefreshProperties.All)]
            [Category("2. Gapfilling")]
            [DisplayName("CDR top n")]
            [Description("maximum number of cdrs to integrate into full length predictions")]
            public int MaxCdrInPrediction { get; set; } = 10;

            [RefreshProperties(RefreshProperties.All)]
            [Category("1. Parsing")]
            [DisplayName("Add Null TermContigs")]
            [Description("Force add a contig with the terminus set to 0 for the n and c terminal regions (fr1 and fr4)")]
            public bool AddNullTermContigs { get; set; } = true;


            [RefreshProperties(RefreshProperties.All)]
            [Category("2. Gapfilling")]
            [DisplayName("Add Reads Based On IMGT Numbering")]
            [Description("Add reads that are numbered as a CDR read for consideration as a gap filler.")]
            public bool AddReadsBasedOnNumber { get; set; } = false;

            [RefreshProperties(RefreshProperties.All)]
            [Category("3. Scoring")]
            [DisplayName("account for Partial reduction")]
            [Description("Force add unreduced disulfide bridges in the cdr3 (position 104 and 155)")]
            public bool Cdr3Bridges { get; set; } = false;

            [RefreshProperties(RefreshProperties.All)]
            [Category("3. Scoring")]
            [DisplayName("Use MultiScore")]
            [Description("Calculate multiscore throughout processing")]
            public bool UseMultiScore { get; set; } = true;


            [RefreshProperties(RefreshProperties.All)]
            [Category("3. Scoring")]
            [DisplayName("Renumber upon recombination")]
            [Description("Renumber all reads for each full length contig")]
            public bool RenumberAllFullLengthContigs { get; set; } = false;

            [RefreshProperties(RefreshProperties.All)]
            [Category("1. Parsing")]
            [DisplayName("Keep unshifted")]
            [Description("keep an unshifted copy of every contig")]
            public bool KeepUnshifted { get; set; } = false;


            [RefreshProperties(RefreshProperties.All)]
            [Category("1. Parsing")]
            [DisplayName("Numbering Tolerance")]
            [Description("The maximum deviation that IMGT numbering can fall short of a border before the read is not included (on each side).")]
            public int NumberingTolerance { get; set; } = 10;


            [RefreshProperties(RefreshProperties.All)]
            [Category("3. Scoring")]
            [DisplayName("Hide identical multiscore sections")]
            [Description("In plots, hide the residues which have identical multiscore coverage. Does not affect scoring.")]
            public bool ClipIdentical { get; set; } = false;

            [RefreshProperties(RefreshProperties.All)]
            [Category("3. Scoring")]
            [DisplayName("Fragment tolerance")]
            [Description("In plots, hide the residues which have identical multiscore coverage. Does not affect scoring.")]
            public int Tolerance { get; set; } = 20;

            [RefreshProperties(RefreshProperties.All)]
            [Category("1. Sliding window")]
            [DisplayName("FR1 Shift left")]
            [Description("How far should the sliding window scoring function shift the FR1 to the left")]
            public double Fr1SwShiftLeft { get; set; } = 19;

            [RefreshProperties(RefreshProperties.All)]
            [Category("1. Sliding window")]
            [DisplayName("FR1 Shift right")]
            [Description("How far should the sliding window scoring function shift the FR1 to the right")]
            public double Fr1SwShiftRight { get; set; } = 19;

            [RefreshProperties(RefreshProperties.All)]
            [Category("1. Sliding window")]
            [DisplayName("FR1 step size")]
            [Description("How far should the sliding window scoring function shift the FR1 to the right")]
            public double Fr1SwStepSize { get; set; } = .001;

            [RefreshProperties(RefreshProperties.All)]
            [Category("1. Sliding window")]
            [DisplayName("FR1 Shift left")]
            [Description("How far should the sliding window scoring function shift the FR1 to the left")]
            public double SwShiftLeft { get; set; } = 200;

            [RefreshProperties(RefreshProperties.All)]
            [Category("1. Sliding window")]
            [DisplayName("Shift left")]
            [Description("How far should the sliding window scoring function shift the FR1 to the right")]
            public double SwShiftRight { get; set; } = 200;

            [RefreshProperties(RefreshProperties.All)]
            [Category("1. Sliding window")]
            [DisplayName("Sliding window step size")]
            [Description("How far should the sliding window scoring function shift the FR1 to the right")]
            public double SwStepSize { get; set; } = .01;
            public bool MedianShift { get; set; } = true;

            public Settings()
            {
                SequenceSourceToDisplay = new Dictionary<SequenceSource, bool>();
                OrderingVars = new Dictionary<RegionType, int[]>();

                // initially we only show contigs
                foreach (var source in Enum.GetValues(typeof(SequenceSource)))
                {
                    SequenceSource curSource = (SequenceSource)source;
                    switch (curSource)
					{
						case SequenceSource.Consensus:
							SequenceSourceToDisplay.Add(curSource, false);
							break;
						case SequenceSource.Reads:
							SequenceSourceToDisplay.Add(curSource, true);
							break;
						case SequenceSource.Contig:
							SequenceSourceToDisplay.Add(curSource, false);
							break;
						case SequenceSource.TwoReads:
							SequenceSourceToDisplay.Add(curSource, true);
							break;
						default:
							break;
					}
				}

                foreach (var region in Helpers.GetAllRegionEnums())
                {
                    switch (region)
                    {
                        case RegionType.FR1:
                            OrderingVars.Add(region, new int[] { spectrum_key, multi_key });
                            break;
                        case RegionType.CDR1:
                            OrderingVars.Add(region, new int[] { spectrum_key, peaks_key });
                            break;
                        case RegionType.FR2:
                            OrderingVars.Add(region, new int[] { spectrum_key, multi_key });
                            break;
                        case RegionType.CDR2:
                            OrderingVars.Add(region, new int[] { spectrum_key, peaks_key });
                            break;
                        case RegionType.FR3:
                            OrderingVars.Add(region, new int[] { spectrum_key, multi_key });
                            break;
                        case RegionType.CDR3:
                            OrderingVars.Add(region, new int[] { spectrum_key, peaks_key, conservedness_key });
                            break;
                        case RegionType.FR4:
                            OrderingVars.Add(region, new int[] { spectrum_key, multi_key });
                            break;
                        default:
                            break;
                    }
                }
			}

            //public void WriteAsJson(string path)
            //{
            //    var dir = Path.GetDirectoryName(path) + "\\Settings";
            //    Directory.CreateDirectory(dir);
            //TODO Used old json implementation and was removed as a function.

            //    JsonUtils.WriteDictionary(dir + @"SequenceSourceToDisplay.json", SequenceSourceToDisplay);
            //    JsonUtils.WriteDictionary(dir + @"OrderingVars.json", OrderingVars);
            //    JsonUtils.Write(dir + @"MaxNDelta.json", MaxNDelta);
            //    JsonUtils.Write(dir + @"MaxCDelta.json", MaxCDelta);
            //    JsonUtils.Write(dir + @"ToleranceInDaGapFillers.json", ToleranceInDaGapFillers);
            //    JsonUtils.Write(dir + @"MaxCdrInPrediction.json", MaxCdrInPrediction);
            //    JsonUtils.Write(dir + @"AddNullTermContigs.json", AddNullTermContigs);
            //}

            public static Settings ReadFromJson(string path)
            {
                var dir = Path.GetDirectoryName(path) + "\\Settings";
                Directory.CreateDirectory(dir);

				var set = new Settings
				{
					SequenceSourceToDisplay = JsonUtils.ReadDictionary<SequenceSource, bool>(dir + @"SequenceSourceToDisplay.json"),
					OrderingVars = JsonUtils.ReadDictionary<RegionType, int[]>(dir + @"OrderingVars.json"),
					MaxNDelta = JsonUtils.Read<double>(dir + @"MaxNDelta.json"),
					MaxCDelta = JsonUtils.Read<double>(dir + @"MaxCDelta.json"),
					ToleranceInDaGapFillers = JsonUtils.Read<int>(dir + @"ToleranceInDaGapFillers.json"),
					MaxCdrInPrediction = JsonUtils.Read<int>(dir + @"MaxCdrInPrediction.json"),
					AddNullTermContigs = JsonUtils.Read<bool>(dir + @"AddNullTermContigs.json")
				};

				return set;
            }
		}

        public class Input
		{
            public string Name = "FabLab";
            public string Root = "";
            public string TemplatePath;
            public string ContaminantsPath;
            public string PeaksPath;
            public string SpectrumPath;
            public string ContigPath;

			public Input(string name, string root, string templatePath, string contaminantsPath, string peaksPath, string spectrumPath, string contigPath)
			{
				Name = name;
				Root = root;
				TemplatePath = templatePath;
				ContaminantsPath = contaminantsPath;
                PeaksPath = peaksPath;
				SpectrumPath = spectrumPath;
				ContigPath = contigPath;
			}

			public Input()
			{
			}
		}

        #region Processing

        /// <summary>
        /// Clip and rank all full contigs in the document.
        /// </summary>
        private void ClipAndRank()
		{
			Peptide[] inPep = FullContigs.Select(x => x.Match).ToArray();

			ClipAndRank(inPep);
		}

        /// <summary>
        /// ClipAndRank the input peptides. If the document already had clipped contigs, the resulting contigs will be added.
        /// </summary>
        /// <param name="inPep"></param>
        public void ClipAndRank(Peptide[] inPep)
		{
			List<(string read, double[] numbering)> numbered = ReadFilter.GetNumberingAndAlignmentForReadsDynamicProgramming(inPep.Select(x => x.Sequence), Templates.First());

            var numberedPeps = numbered.Zip(inPep, (numbering, pep) => (pep, numbering.numbering)).ToList();
            var seqSource = SequenceSource.Contig;

            foreach (var region in Helpers.GetAllRegionEnums())
            {
                if (region == RegionType.None)
                    continue;
                int toleranceForRenumbering = 2;
                List<(Peptide clipped, double[] numbering)> clipped1 = ClipToRegion(numberedPeps, region, false, toleranceForRenumbering).Where(x => x.clipped.Length != 0).ToList();

                // TODO chance for a speedup possibly
                int toleranceForClipping = 0;
				List<(Peptide pep, double[] numbering)> renumbered = ReadFilter.GetNumberingAndAlignmentForReadsDynamicProgramming(clipped1.Select(x => x.clipped.Sequence), Templates.First()).Zip(clipped1, (numbering, pep) => (pep.clipped, numbering.numbering)).ToList();
                List<(Peptide clipped, double[] numbering)> contigs = ClipToRegion(renumbered, region, true, toleranceForClipping).Where(x => x.clipped.Length != 0).ToList();

                (Peptide x, double[])[] noDups2 = RemoveDuplicates(contigs);
                //(Peptide x, double[])[] noDups2 = contigs.ToArray();

                var ranked = Helpers.Rank(noDups2, this, seqSource, region);

				// we only want FRs of at least length 3
                if (!Helpers.IsCdr(region))
					ranked = ranked.Where(x => x.contig.Length > 2).ToArray();

                if (ClippedContigs.TryGetValue(region, out RankedContig[] previouslyRanked))
				{
					var rList = ranked.ToList();
					rList.AddRange(previouslyRanked);

					// remove duplicates, this is not perfect! //TODO
					rList = rList.GroupBy(x => x.contig.GetModifiedSequence()).Select(x => x.First()).ToList();

                    var varsToOrderOn = CurrentSettings.OrderingVars[region];
                    if (varsToOrderOn.Contains(5) && !CurrentSettings.UseMultiScore)
                    {
                        var l = varsToOrderOn.ToList();
                        l.Add(1);
                        varsToOrderOn = l.ToArray();
                    }
                    
                    ClippedContigs[region] = Reorder(rList.ToArray(), varsToOrderOn);
                }
				else
				{
					ClippedContigs.Add(region, ranked);
				}
			}

			(Peptide x, double[])[] RemoveDuplicates(List<(Peptide clipped, double[] numbering)> contigs)
			{
				var noDups = PeptideExtender.RemoveDuplicatePeptides(contigs.Select(x => {
                    x.clipped.Name = "";
                    return x.clipped;
                }), Spectrum).Where(x => x.Sequence != "");
				var noDups2 = noDups.Select(x => (x, contigs.Where(y => y.clipped.GetModifiedSequence() == x.GetModifiedSequence()).First().numbering)).ToArray();
				return noDups2;
			}

			if (CurrentSettings.MedianShift)
			{
                var bestFr4 = ClippedContigs[RegionType.FR4].OrderByDescending(x => x.spectrum).First().contig;
                var asm = new AnnotatedSpectrumMatch(Spectrum, bestFr4, ScoringModel);
                var relErrors = GatherErrorsAndCorrect(asm.FragmentMatches, Spectrum);

                foreach (var region in Helpers.GetAllRegionEnums())
                {
                    if (region == RegionType.None)
                        continue;

                    if (Helpers.IsCdr(region))
                        continue;

                    if (ClippedContigs.TryGetValue(region, out RankedContig[] previouslyRanked))
                    {
                        ClippedContigs[region] = Helpers.Rank(previouslyRanked.Select(x => (x.contig.ShiftPeptideToOptimum(Spectrum, ScoringModel, Math.Abs(2 * relErrors.Average() * Spectrum.PrecursorMass), Math.Abs(2 * relErrors.Average() * Spectrum.PrecursorMass), stepSize: 0.001), x.numbering)).ToArray(), this, seqSource, region);
                    }
                }
            }
            
        }

        private static double[] GatherErrorsAndCorrect(PeptideFragment[] matches, SpectrumContainer hcSpectrum)
        {
            List<double> errorsHc = new List<double>();
            List<double> relErrorsHc = new List<double>();

            for (int i = 0; i < matches.Length; i++)
            {
                if (matches[i] != null)
                {
                    double diff = matches[i].Mz - hcSpectrum.Peaks[i].Mz;
                    double relDiff = diff / hcSpectrum.Peaks[i].Mz;
                    errorsHc.Add(diff);
                    relErrorsHc.Add(relDiff);
                }
            }
            var relErrorArrayHc = relErrorsHc.ToArray();

            var correction = Statistics.Median(relErrorsHc.ToArray());

            List<double> errorsAfterCorrectionHc = new List<double>();
            List<double> relErrorsAfterCorrectionHc = new List<double>();
            for (int i = 0; i < matches.Length; i++)
            {
                hcSpectrum.Peaks[i].Mz = hcSpectrum.Peaks[i].Mz * (1 + correction);
                hcSpectrum.Peaks[i].MinMz = (float)(hcSpectrum.Peaks[i].MinMz * (1 + correction));
                hcSpectrum.Peaks[i].MaxMz = (float)(hcSpectrum.Peaks[i].MaxMz * (1 + correction));
                if (matches[i] != null)
                {
                    double diff = matches[i].Mz - hcSpectrum.Peaks[i].Mz;
                    double relDiff = diff / hcSpectrum.Peaks[i].Mz * 1000000;
                    errorsAfterCorrectionHc.Add(diff);
                    relErrorsAfterCorrectionHc.Add(relDiff);
                }
            }

            //var errorsAfterCorrectionArrayHc = errorsAfterCorrectionHc.ToArray();
            //var relErrorsAfterCorrectionArrayHc = relErrorsAfterCorrectionHc.ToArray();
            return relErrorArrayHc;
        }

        public List<(Peptide clipped, double[] numbering)> ClipToRegion(List<(Peptide pep, double[] numbering)> numberedPeps, RegionType curRegion, bool shift = true, int tolerance = 0)
		{
			var (startImgt, endImgt) = Helpers.GetBorders(curRegion);

			var contigs = new List<(Peptide clipped, double[] numbering)>();

			foreach (var x in numberedPeps)
			{
				Peptide contig = x.pep;
				var length = contig.Length;

				double[] numbering = x.numbering;
				var idxStartRegion = numbering.IndexOf(startImgt);
				idxStartRegion = idxStartRegion == -1 ? 0 : idxStartRegion;

                // cant be below zero
				if (idxStartRegion <= tolerance)
				{
                    idxStartRegion = 0;
				}
				else
				{
                    idxStartRegion -= tolerance;
				}

				var idxEndRegion = numbering.IndexOf(endImgt);
				idxEndRegion = idxEndRegion == -1 ? length - 1 : idxEndRegion;

                // cant be above length - 1
                if (idxEndRegion >= length - tolerance - 1)
                {
                    idxEndRegion = length - 1;
                }
                else
                {
                    idxEndRegion += tolerance;
                }

                var clipped = contig.TrimTermini(out _, idxStartRegion, length - idxEndRegion - 1);
				double[] innerNumbering = numbering.Skip(idxStartRegion).Take(clipped.Length).ToArray();

                if (!innerNumbering.Min().IsBetweenInclusive(startImgt - tolerance - 0.01, endImgt + 0.01) || !innerNumbering.Max().IsBetweenInclusive(startImgt - 0.01, endImgt + tolerance + 0.01))
                {
                    clipped.Sequence = "";
                }

                if (shift)
				{
					if (clipped.Sequence.Length > 1)
					{
						PeptideFragment.FragmentModel innerModel = ScoringModel;

						if (curRegion == RegionType.FR1)
						{
							innerModel.NTerminalOffSetIons = false;
						}
						if (curRegion == RegionType.FR4)
                        {
							if (CurrentSettings.KeepUnshifted)
							{
                                // we also add the unshifted version, as it is possible that the shift is at the end of the contig which would not affect the clipped contigs placement.
                                contigs.Add((SpectrumUtils.ResizeFromTerminus(Proteomics.Terminus.C, clipped, Spectrum, innerModel, -100, 100, topXToReturn: 20, shift: false), innerNumbering));
                            }

                            innerModel.CTerminalOffSetIons = false;
							clipped = SpectrumUtils.ResizeFromTerminus(Proteomics.Terminus.C, clipped, Spectrum, innerModel, -100, 100, topXToReturn: 20);
							if (innerNumbering.Last().IsBetweenInclusive(endImgt - 0.01, endImgt + 0.01))
							{
								Peptide nullBorder = (Peptide)clipped.Clone();

								nullBorder.Nterm.Delta += clipped.Cterm.Delta;
								nullBorder.Cterm.Delta = 0;
								contigs.Add((nullBorder, innerNumbering));
							}
						}
						else
                        {
                            if (CurrentSettings.KeepUnshifted)
                            {
                                // we also add the unshifted version, as it is possible that the shift is at the end of the contig which would not affect the clipped contigs placement.
                                contigs.Add((SpectrumUtils.ResizeFromTerminus(Proteomics.Terminus.N, clipped, Spectrum, innerModel, -200, 200, topXToReturn: 20, shift: false), innerNumbering));
                            }
							clipped = SpectrumUtils.ResizeFromTerminus(Proteomics.Terminus.N, clipped, Spectrum, innerModel, -200, 200, topXToReturn: 20);
						}
						if (curRegion == RegionType.FR1)
						{
							if (innerNumbering.First().IsBetweenInclusive(1 - 0.01, 1 + 0.01))
							{
								Peptide nullBorder = (Peptide)clipped.Clone();

								nullBorder.Cterm.Delta += clipped.Nterm.Delta;
								nullBorder.Nterm.Delta = 0;
								contigs.Add((nullBorder, innerNumbering));


                                innerModel.Z = null;

                                clipped = ((Peptide)nullBorder.Clone()).ShiftPeptideToOptimumNTerm(Spectrum, innerModel, CurrentSettings.Fr1SwShiftLeft, CurrentSettings.Fr1SwShiftRight, stepSize: CurrentSettings.Fr1SwStepSize);
                            }
						}
						else
						{
                            clipped = clipped.ShiftPeptideToOptimum(Spectrum, innerModel, CurrentSettings.SwShiftLeft, CurrentSettings.SwShiftRight, stepSize: CurrentSettings.SwStepSize);
                        }
					}
				}
				contigs.Add((clipped, innerNumbering));
			}

			return contigs;
		}

		#endregion

		#region Saving

		/// <summary>
		/// Save the processed Data. 
		/// </summary>
		public void SaveProcessed()
        {
            //var sfd = new SaveFileDialog()
            //{
            //    AddExtension = true,
            //    DefaultExt = ".flab",
            //};

            //if (sfd.ShowDialog() == DialogResult.OK)
            //{
            //    try
            //    {
            //        var dir = Path.GetDirectoryName(sfd.FileName) + "\\Save";
            //        Directory.CreateDirectory(dir);
            //        JsonUtils.Write(dir + @"\predictions.json", Predictions);
            //        JsonUtils.Write(dir + @"\numberedTemplates.json", NumberedTemplates);
            //        JsonUtils.Write(dir + @"\readFilter.json", ReadFilter);
            //TODO Used old json implementation and was removed as a function.
            //        JsonUtils.WriteDictionary(dir + @"\clippedContigs.json", ClippedContigs);
            //        JsonUtils.WriteDictionary(dir + @"\fillerDict.json", FillerDict);
            //        JsonUtils.Write(dir + @"\spectrum.json", Spectrum);
            //        CurrentSettings.WriteAsJson(dir + @"\settings.json");

            //        if (File.Exists(sfd.FileName))
            //            File.Delete(sfd.FileName);

            //        ZipFile.CreateFromDirectory(dir, sfd.FileName);

            //        if (Directory.Exists(dir))
            //            Directory.Delete(dir, true);
            //    }
            //    catch (Exception e)
            //    {
            //        MessageBox.Show($"failed to save: {e.Message}");
            //    }
            //}
        }

        /// <summary>
        /// Save the processed Data
        /// </summary>
        public void SaveInput()
        {
            var sfd = new SaveFileDialog();

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                var dir = Path.GetDirectoryName(sfd.FileName) + "\\Save";
                Directory.CreateDirectory(dir);

                FastaIO.WriteFasta(Templates.Select(x => x.Sequence), Templates.Select(x => x.Name), dir + @"\templates.fasta");
                FastaIO.WriteFasta(ReadFilter.Templates.Where(x => !Templates.Contains(x)).Select(x => x.Sequence), ReadFilter.Templates.Where(x => !Templates.Contains(x)).Select(x => x.Name), dir + @"\contaminants.fasta");
                JsonUtils.Write(dir + @"\spectrum.json", Spectrum);

				string mgfPath = dir + @"\spectrum.mgf";
				StreamWriter spectrumWriter = new StreamWriter(mgfPath);
                MgfRawFile.WriteSpectrum(spectrumWriter, Spectrum.Precursor.RawFile, Spectrum.Precursor, Spectrum.Peaks, HeckLib.masspec.Spectrum.PolarityType.Positive);
                spectrumWriter.Flush();
                spectrumWriter.Close();


                JsonUtils.Write(dir + @"\spectrum.json", Spectrum);
                FastaIO.WriteProForma(FullContigs.Select(x => x.Match), dir + @"\fullContigs.proforma");
                CsvFileWriter<PeaksPeptideData> w = new HeckLib.io.fileformats.CsvFileWriter<PeaksPeptideData>(dir + @"\peaks.csv");
                
                foreach (var item in ReadFilter.IndexedReads)
                {
                    var corr = new PeaksPeptideData(item)
                    {
                        Peptide = item.OriginalSequence,
                    };
                    w.Write(corr);
                }
                w.Dispose();
            }
        }

		#endregion

		#region loading

		/// <summary>
		/// load previously processed files through the file dialog
		/// </summary>
		/// <returns></returns>
        public static Document LoadProcessed(Settings settings = null)
        {
            var ofd = new OpenFileDialog()
            {
                DefaultExt = ".flab"
            };

            Document doc = new Document();
			if (settings != null)
			{
                doc.CurrentSettings = settings;
			}

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                doc = LoadProcessed(ofd.FileName);
                doc.Title = Path.GetFileNameWithoutExtension(ofd.FileName);
            }
            else
                return null;

            return doc;
        }

        /// <summary>
        /// create a config file
        /// </summary>
        /// <returns></returns>
        public static Document LoadFromPaths(Settings settings)
        {
            var ofd = new OpenFileDialog();
			MessageBox.Show("Select fasta with templates (all files should be in the same folder)");
			ofd.Title = "Select Templates";
			string templatePath;
			if (ofd.ShowDialog() == DialogResult.OK)
			{
				templatePath = Path.GetFileName(ofd.FileName);
			}
			else
				return null;

			MessageBox.Show("Select fasta with contaminants (all files should be in the same folder)");
            ofd.Title = "Select Contaminants";

			string contaminantsPath;
			if (ofd.ShowDialog() == DialogResult.OK)
			{
				contaminantsPath = Path.GetFileName(ofd.FileName);
			}
			else
				return null;

			MessageBox.Show("Select de novo peptides from peaks (all files should be in the same folder)");
            ofd.Title = "Select reads";
			string peaksPath;
			if (ofd.ShowDialog() == DialogResult.OK)
			{
				peaksPath = Path.GetFileName(ofd.FileName);
			}
			else
				return null;

			MessageBox.Show("Select top down spectra (mgf) (all files should be in the same folder)");
            ofd.Title = "Select spectrum";
			string spectrumPath;
			if (ofd.ShowDialog() == DialogResult.OK)
			{
				spectrumPath = Path.GetFileName(ofd.FileName);
			}
			else
				return null;

			MessageBox.Show("Select contigs file (pro forma) (all files should be in the same folder)");
            ofd.Title = "Select contigs";
			string contigPath;
			if (ofd.ShowDialog() == DialogResult.OK)
			{
				contigPath = Path.GetFileName(ofd.FileName);
			}
			else
				return null;

			var input = new Input("fablab", Path.GetDirectoryName(ofd.FileName), templatePath, contaminantsPath, peaksPath, spectrumPath, contigPath);

			var sfd = new SaveFileDialog
			{
				DefaultExt = "flconfig"
			};
			MessageBox.Show("select write location for configfile");
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                JsonUtils.Write(sfd.FileName, input);
            }
            else
                return null;

            return LoadFromInputClass(input, settings);
        }

        public static Document LoadFromInputClassPath(string inputPath, Settings settings)
        {
			Input input = JsonUtils.Read<Input>(inputPath);
			if (input.Root == string.Empty)
                input.Root = Path.GetDirectoryName(inputPath);
			
            return LoadFromInputClass(input, settings);
        }

        /// <summary>
        /// create a config file
        /// </summary>
        /// <returns></returns>
        public static Document LoadFromInputClass(Input input, Settings settings)
		{
			Document doc = new Document
			{
				CurrentSettings = settings
			};

			var templates = new List<Peptide>();
			var contaminants = new List<Peptide>();
			PeaksPeptideData[] peaks = new PeaksPeptideData[0];

            // support full paths and relative paths
			if (input.Root != "")
			{
                Directory.SetCurrentDirectory(input.Root);
            }

			try
			{
                templates = FastaIO.ReadFasta(input.TemplatePath).Select(x => new Peptide(x.Value, x.Key)).ToList();
			}
			catch (Exception e)
			{

				throw;
			}
			contaminants = FastaIO.ReadFasta(input.ContaminantsPath).Select(x => new Peptide(x.Value, x.Key)).ToList();
			peaks = CsvFileReader<PeaksPeptideData>.ParseAll(input.PeaksPath);
			contaminants.AddRange(templates);
			doc.ReadFilter = new ReadFilter(peaks, contaminants);
			doc.ReadFilter.Process();
			doc.NumberedTemplates = templates.Select(x => doc.ReadFilter.TemplateNumbering[doc.ReadFilter.GetTemplateIdx(x)]).Select(x => (x.template, x.numbering)).ToList();
			doc.NumberedReads = doc.ReadFilter.GetRenumberedReadsForTemplates(doc.Templates);
            if (doc.Locus == LociEnum.TBD)
                doc.Locus = Helpers.GetLocusEnum(doc.Templates.Last().Name);

            // spectrum is a json
			if (input.SpectrumPath.EndsWith("json"))
			{
                doc.Spectrum = JsonUtils.Read<SpectrumContainer>(input.SpectrumPath);
            }
            // spectrum should be mgf
			else
			{
                Dictionary<PrecursorInfo, HeckLib.Centroid[]> mgfFile = MgfRawFile.ParseWithPrecursors(input.SpectrumPath);
                doc.Spectrum = new SpectrumContainer(mgfFile.Values.First(), mgfFile.Keys.First());
                DuplicateHanding.RemoveDuplicates(ref doc.Spectrum);
                doc.Spectrum.Precursor.Fragmentation = HeckLib.masspec.Spectrum.FragmentationType.ETD;
            }
			doc.FullContigs = new List<(Peptide Match, List<ExtenderBase<Peptide>.MetaData<int>>)>();
			HeckLib.io.fasta.FastaParser.Parse(input.ContigPath, HeckLib.io.fasta.FastaParser.Format.PROFORMA, Modification.Parse(), delegate (string header, string sequence, Modification nterm, Modification cterm, Modification[] modifications, double localprogress, out bool cancel)
			{
				Peptide pep = new Peptide(sequence, nterm, cterm, modifications, header);
				doc.FullContigs.Add((pep, null));
				cancel = false;
			});

			AddConsensusToDoc(doc);

			doc.ClipAndRank();
            doc.Title = input.Name;
			return doc;
		}

        private static void AddConsensusToDoc(Document doc)
		{
			var consensus = ReadFilter.GetConsensusFromReadsOriginal(doc.NumberedReads).Where(x => x.frequencies.Count != 0).ToDictionary(x => x.number, x => x.frequencies.OrderByDescending(y => y.count).ToList());
			var nums = consensus.Keys;
			var orderedNums = Helpers.OrderImgtNumbering(nums);
			doc.Consensus = orderedNums.Select(x => (x, consensus[x])).ToList();
		}

		/// <summary>
		/// Load previously processed data
		/// </summary>
		/// <param name="fileName"></param>
		/// <returns></returns>
        public static Document LoadProcessed(string fileName)
        {
            string writePath = Path.GetTempPath() + @"\fablab";
            if (Directory.Exists(writePath))
                Directory.Delete(writePath, true);
            Directory.CreateDirectory(writePath);

            ZipFile.ExtractToDirectory(fileName, writePath);

            Document doc = new Document();
            var actionList = new Action[]
            {
                new Action(() => { doc.Predictions = JsonUtils.Read<RankedContig[]>(writePath + @"\predictions.json"); }),
                new Action(() => { doc.NumberedTemplates = JsonUtils.Read<List<(Peptide read, double[] numbering)>>(writePath + @"\numberedTemplates.json"); }),
                new Action(() => { doc.ReadFilter = JsonUtils.Read<ReadFilter>(writePath + @"\readFilter.json");
                    doc.NumberedReads = doc.ReadFilter.GetRenumberedReadsForTemplates(doc.Templates);
                }),
                new Action(() => { doc.ClippedContigs = JsonUtils.ReadDictionary<RegionType, RankedContig[]>(writePath + @"\clippedContigs.json"); }),
                new Action(() => { doc.Spectrum = JsonUtils.Read<SpectrumContainer>(writePath + @"\Spectrum.json"); }),
                new Action(() => { doc.CurrentSettings = Settings.ReadFromJson(writePath + @"\settings.json"); }),
            };

            Parallel.Invoke(actionList);
            AddConsensusToDoc(doc);
            doc.Title = Path.GetFileNameWithoutExtension(fileName);

            return doc;
        }

        #endregion


        #region Helpers

        /// <summary>
        /// Reorders based on scores, so only ranks are changed. accounts for settings for the region
        /// </summary>
        /// <param name="region"></param>
        /// <param name="contigs"></param>
        /// <returns></returns>
        public static RankedContig[] Reorder(RankedContig[] contigs, int[] varsToOrderOn)
        {
            var uniquePeaksScore = contigs.Select(x => x.peaks).Distinct().OrderByDescending(x => x).ToArray();
            var uniqueTemplateScore = contigs.Select(x => x.template).Distinct().OrderByDescending(x => x).ToArray();
            var uniqueConservationScore = contigs.Select(x => x.conservedness).Distinct().OrderByDescending(x => x).ToArray();
            var uniqueSpectrumScore = contigs.Select(x => x.spectrum).Distinct().OrderByDescending(x => x).ToArray();
            var uniqueMultiScore = contigs.Select(x => x.multi).Distinct().OrderByDescending(x => x).ToArray();

            ConcurrentDictionary<double, int> peaksRanks = new ConcurrentDictionary<double, int>();
            ConcurrentDictionary<double, int> templateRanks = new ConcurrentDictionary<double, int>();
            ConcurrentDictionary<double, int> SpectrumRanks = new ConcurrentDictionary<double, int>();
            ConcurrentDictionary<double, int> conservationRanks = new ConcurrentDictionary<double, int>();
            ConcurrentDictionary<double, int> multiRanks = new ConcurrentDictionary<double, int>();

            int maxLength = new int[] { uniquePeaksScore.Length, uniqueConservationScore.Length, uniqueSpectrumScore.Length, uniqueTemplateScore.Length }.Max();

            Parallel.For(
                0,
                uniquePeaksScore.Length,
                i =>
                {
                    peaksRanks.AddOrUpdate(uniquePeaksScore[i], i, (k, v) => v);
                });
            Parallel.For(
                0,
                uniqueTemplateScore.Length,
                i =>
                {
                    templateRanks.AddOrUpdate(uniqueTemplateScore[i], i, (k, v) => v);
                });
            Parallel.For(
                0,
                uniqueConservationScore.Length,
                i =>
                {
                    conservationRanks.AddOrUpdate(uniqueConservationScore[i], i, (k, v) => v);
                });
            Parallel.For(
                0,
                uniqueMultiScore.Length,
                i =>
                {
                    multiRanks.AddOrUpdate(uniqueMultiScore[i], i, (k, v) => v);
                });
            Parallel.For(
                0,
                uniqueSpectrumScore.Length,
                i =>
                {
                    SpectrumRanks.AddOrUpdate(uniqueSpectrumScore[i], i, (k, v) => v);
                });


            int contigCount = contigs.Count();
            Parallel.For(0, contigCount, i => {
                contigs[i].peaksR = peaksRanks[contigs[i].peaks];
                contigs[i].templateR = templateRanks[contigs[i].template];
                contigs[i].spectrumR = SpectrumRanks[contigs[i].spectrum];
                contigs[i].conservednessR= conservationRanks[contigs[i].conservedness];
                contigs[i].multiR = multiRanks[contigs[i].multi];
                contigs[i].sumR = 0;
            });
                    
        Parallel.For(0, contigs.Length, i =>
            {
                if (varsToOrderOn.Contains(peaks_key))
                {
                    contigs[i].sumR += contigs[i].peaksR;
                }
                if (varsToOrderOn.Contains(template_key))
                {
                    contigs[i].sumR += contigs[i].templateR; // never hit
                }
                if (varsToOrderOn.Contains(spectrum_key))
                {
                    contigs[i].sumR += contigs[i].spectrumR;
                }
                if (varsToOrderOn.Contains(conservedness_key))
                {
                    contigs[i].sumR += contigs[i].conservednessR;
                }
                if (varsToOrderOn.Contains(multi_key))
                {
                    contigs[i].sumR += contigs[i].multiR;
                }
            });

            return contigs.OrderBy(x => x.sumR).ThenBy(x => x.templateR).ThenBy(x => x.conservednessR).ToArray();
        }

        #endregion


        #region embedded resources

        /// <summary>
        /// Path to embedded IMGT amino acid frequency table for Kappa light chains
        /// </summary>
        public static string IgKProbabilityDistributionPath { get => ParseResource(IgKProbabilityDistribution); }
        private const string IgKProbabilityDistribution = "IMGT_NoIsotypes_IGK_probabilitydistribution.csv";

        /// <summary>
        /// Path to embedded IMGT amino acid frequency table for Lambda light chains
        /// </summary>
        public static string IgLProbabilityDistributionPath { get => ParseResource(IgLProbabilityDistribution); }
        private const string IgLProbabilityDistribution = "IMGT_NoIsotypes_IGL_probabilitydistribution.csv";

        /// <summary>
        /// Path to embedded IMGT amino acid frequency table for heavy chains
        /// </summary>
        public static string IgHProbabilityDistributionPath { get => ParseResource(IgHProbabilityDistribution); }
        private const string IgHProbabilityDistribution = "IMGT_NoIsotypes_IGH_probabilitydistribution.csv";

        private static string ParseResource(string filename)
        {
            string file = HeckLibSettings.CreateAppDataFilename(filename, HeckLibSettings.FolderType.RESOURCE);

            if (!File.Exists(file))
                ExportEmbeddedResourceToCentralLocation(file, filename);
            else if (File.ReadAllText(file) == string.Empty)
                ExportEmbeddedResourceToCentralLocation(file, filename);

            return file;
        }

        private static void ExportEmbeddedResourceToCentralLocation(string fileName, string ResourceStreamName)
        {
            using (FileStream file = File.Create(fileName))
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                Stream stream = assembly.GetManifestResourceStream("FabLab.Assets." + ResourceStreamName);
                FilesA.CopyStream(stream, file);
            }
        }

        #endregion
    }

    public struct RankedContig : IEquatable<RankedContig>
    {
        public Peptide contig;
        public double peaks;
        public double template;
        public double spectrum;
        public double conservedness;
        public double multi;
        public int peaksR;
        public int templateR;
        public int spectrumR;
        public int conservednessR;
        public int multiR;
        public int sumR;
        public double[] numbering;
        public SequenceSource origin;

        public RankedContig(Peptide contig, double peaks, double template, double spectrum, double conservedness, int peaksR, int templateR, int spectrumR, int conservednessR, int sumR, double[] numbering, SequenceSource source)
        {
            this.contig = contig;
            this.peaks = peaks;
            this.template = template;
            this.spectrum = spectrum;
            this.conservedness = conservedness;
            this.peaksR = peaksR;
            this.templateR = templateR;
            this.spectrumR = spectrumR;
            this.conservednessR = conservednessR;
            this.sumR = sumR;
            this.numbering = numbering;
            this.origin = source;
            this.multi = 0;
            this.multiR = 0;
        }

		public override bool Equals(object obj)
        {
            // we dont compare ranks because they are relative
            return obj is RankedContig other &&
                   EqualityComparer<Peptide>.Default.Equals(contig, other.contig) &&
                   peaks == other.peaks &&
                   template == other.template &&
                   spectrum == other.spectrum &&
                   conservedness == other.conservedness &&
                   multi == other.multi  &&
                   //peaksR == other.peaksR &&
                   //templateR == other.templateR &&
                   //spectrumR == other.spectrumR &&
                   //conservednessR == other.conservednessR &&
                   //sumR == other.sumR &&
                   EqualityComparer<double[]>.Default.Equals(numbering, other.numbering);
        }

        public override int GetHashCode()
        {
            int hashCode = 1287887247;
            hashCode = hashCode * -1521134295 + EqualityComparer<Peptide>.Default.GetHashCode(contig);
            hashCode = hashCode * -1521134295 + peaks.GetHashCode();
            hashCode = hashCode * -1521134295 + template.GetHashCode();
            hashCode = hashCode * -1521134295 + spectrum.GetHashCode();
            hashCode = hashCode * -1521134295 + multi.GetHashCode();
            hashCode = hashCode * -1521134295 + conservedness.GetHashCode();
            hashCode = hashCode * -1521134295 + peaksR.GetHashCode();
            hashCode = hashCode * -1521134295 + templateR.GetHashCode();
            hashCode = hashCode * -1521134295 + spectrumR.GetHashCode();
            hashCode = hashCode * -1521134295 + multiR.GetHashCode();
            hashCode = hashCode * -1521134295 + conservednessR.GetHashCode();
            hashCode = hashCode * -1521134295 + sumR.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<double[]>.Default.GetHashCode(numbering);
            return hashCode;
        }

        public void Deconstruct(out Peptide contig, out double peaks, out double template, out double spectrum, out double conservedness, out int peaksR, out int templateR, out int spectrumR, out int conservednessR, out int sumR, out double[] numbering)
        {
            contig = this.contig;
            peaks = this.peaks;
            template = this.template;
            spectrum = this.spectrum;
            conservedness = this.conservedness;
            peaksR = this.peaksR;
            templateR = this.templateR;
            spectrumR = this.spectrumR;
            conservednessR = this.conservednessR;
            sumR = this.sumR;
            numbering = this.numbering;
        }

		public bool Equals(RankedContig other)
		{
            // we dont compare ranks because they are relative
            return EqualityComparer<Peptide>.Default.Equals(contig, other.contig) &&
                   peaks == other.peaks &&
                   template == other.template &&
                   spectrum == other.spectrum &&
                   conservedness == other.conservedness &&
                   EqualityComparer<double[]>.Default.Equals(numbering, other.numbering);
        }

		public static implicit operator (Peptide contig, double peaks, double template, double spectrum, double conservedness, int peaksR, int templateR, int spectrumR, int conservednessR, int sumR, double[] numbering, SequenceSource source)(RankedContig value)
        {
            return (value.contig, value.peaks, value.template, value.spectrum, value.conservedness, value.peaksR, value.templateR, value.spectrumR, value.conservednessR, value.sumR, value.numbering, value.origin);
        }

        public static implicit operator RankedContig((Peptide contig, double peaks, double template, double spectrum, double conservedness, int peaksR, int templateR, int spectrumR, int conservednessR, int sumR, double[] numbering, SequenceSource source) value)
        {
            return new RankedContig(value.contig, value.peaks, value.template, value.spectrum, value.conservedness, value.peaksR, value.templateR, value.spectrumR, value.conservednessR, value.sumR, value.numbering, SequenceSource.Contig);
        }
    }
}