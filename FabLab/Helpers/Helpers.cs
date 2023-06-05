using BrightIdeasSoftware;
using HeckLib.chemistry;
using HeckLib.ConvenienceInterfaces.SpectrumMatch;
using HeckLib.masspec;
using HeckLib.objectlistview;
using HeckLib.utils;
using HeckLib.visualization.objectlistview;
using PsrmLib;
using PsrmLib.IO;
using PsrmLib.Models;
using PsrmLib.Processing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static HeckLib.masspec.SpectrumUtils;

namespace FabLab.Helpers
{
    public class Helpers
    {
        private static Dictionary<double[], List<double>> SortedPerInput = new Dictionary<double[], List<double>>();

        public static HecklibOLVColumn CreateColumn(ObjectListView listview, string title, HorizontalAlignment halign, AspectGetterDelegate function, IEnumerable<object> objectsToBe, bool sortable = false, CustomFilterMenuBuilder customFilterMenuBuilder = null)
        {
            HecklibOLVColumn col = new HecklibOLVColumn();
            objectsToBe = objectsToBe ?? new List<object>();
            col.Text = title;
            col.HeaderTextAlign = HorizontalAlignment.Center;
            col.TextAlign = halign;
            if (customFilterMenuBuilder != null)
                col.FilterMenuBuildStrategy = customFilterMenuBuilder;
            col.AspectGetter = function;
            col.Sortable = sortable;
            col.Width = SetColWidth(objectsToBe.Select(x => function(x)), listview, title);

            listview.Columns.Add(col);
            listview.AllColumns.Add(col);
            
            return col;

            int SetColWidth(IEnumerable<object> fields, ObjectListView scoreOlv, string innerTitle)
            {
                List<int> stringLengths = fields.Select(x => x.ToString().Length).ToList();
                //ToEnsure the list has entries
                stringLengths.Add(0);
                return (int)Math.Max(
                                scoreOlv.CreateGraphics().MeasureString(new string(Enumerable.Repeat('A', stringLengths.Max() + 2).ToArray()), scoreOlv.Font).Width,
                                scoreOlv.CreateGraphics().MeasureString(innerTitle + "AA", scoreOlv.Font).Width);
            }
        }

        /// <summary>
        /// account fir I/L
        /// </summary>
        /// <param name="contigs"></param>
        /// <param name="spectrum"></param>
        /// <param name="reads"></param>
        /// <param name="templates"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public static RankedContig[] Rank((Peptide, double[])[] contigs, Document doc, SequenceSource source, RegionType region)
        {
            var res = new ConcurrentBag<RankedContig>();

            string csvPath = Document.IgHProbabilityDistributionPath;
            switch (doc.Locus)
            {
                case ImgtFilterLib.LociEnum.Kappa:
                    csvPath = Document.IgKProbabilityDistributionPath;
                    break;
                case ImgtFilterLib.LociEnum.Lambda:
                    csvPath = Document.IgLProbabilityDistributionPath;
                    break;
                case ImgtFilterLib.LociEnum.Heavy:
                    break;
                case ImgtFilterLib.LociEnum.TBD:
                    break;
                default:
                    break;
            }

			List<(double number, List<(char residue, int count)> frequencies)> distribution = doc.Consensus ?? ReadFilter.GetConsensusFromReadsOriginal(doc.NumberedReads);

            List<(double number, List<(char residue, int count)>)> templateDistribution = ReadFilter.GetConsensusFromReads(doc.NumberedTemplates).ToList();

            Parallel.ForEach(contigs, contig =>
            {
                RankedContig con = new RankedContig();
                con.contig = contig.Item1;
                con.numbering = contig.Item2;
                con.conservedness = ExtenderBase<int>.Scoring.GetConservednessSupportScore((contig.Item1.Sequence, contig.Item2), csvPath);
                con.peaks = ExtenderBase<int>.Scoring.GetPEAKSSupportScore((contig.Item1.Sequence, contig.Item2), distribution);
                con.template = ExtenderBase<int>.Scoring.GetTemplateSupport((contig.Item1.Sequence, contig.Item2), templateDistribution).Sum();

                var asm = new AnnotatedSpectrumMatch(doc.Spectrum, contig.Item1, doc.ScoringModel);
                con.spectrum = ExtenderBase<int>.Scoring.GetSpectralSupportScore(asm);
                con.origin = source;

                con.multi = 0;
                con.multiR = 0;

                res.Add(con);
            }
            );

            var resArray = res.ToArray();

            if (doc.CurrentSettings.UseMultiScore)
                resArray = GetMultiScore(doc, null, res);
            var varsToOrderOn = doc.CurrentSettings.OrderingVars[region];
            if (varsToOrderOn.Contains(5) && !doc.CurrentSettings.UseMultiScore)
            {
                var l = varsToOrderOn.ToList();
                l.Add(1);
                varsToOrderOn = l.ToArray();
            }

            return Document.Reorder(resArray, varsToOrderOn);
        }

        /// <summary>
        /// account for the weights given in settings, because we cannot do so through the front end. no support for spectral score so far, and integrated support for conservedness so we do not acount for it
        /// </summary>
        /// <param name="contigs"></param>
        /// <param name="spectrum"></param>
        /// <param name="reads"></param>
        /// <param name="templates"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public static RankedContig[] RankFullLength((Peptide, double[])[] contigs, SequenceSource source, Document doc, ImgtFilterLib.LociEnum locus = ImgtFilterLib.LociEnum.Kappa, int[] multiScoreRange = null)
		{
			var res = new ConcurrentBag<RankedContig>();

			string csvPath = Document.IgHProbabilityDistributionPath;
			switch (locus)
			{
				case ImgtFilterLib.LociEnum.Kappa:
					csvPath = Document.IgKProbabilityDistributionPath;
					break;
				case ImgtFilterLib.LociEnum.Lambda:
					csvPath = Document.IgHProbabilityDistributionPath;
					break;
				case ImgtFilterLib.LociEnum.Heavy:
					break;
				case ImgtFilterLib.LociEnum.TBD:
					break;
				default:
					break;
			}

			var distribution = ReadFilter.GetConsensusFromReadsOriginal(doc.NumberedReads);
			List<(double number, List<(char residue, int count)>)> templateDistribution = ReadFilter.GetConsensusFromReads(doc.NumberedTemplates.Select(x => (x.read.Sequence, x.numbering)).ToList());

			// we remove the support from regions where it should not be accounted for in the score.
			foreach (var region in Enum.GetValues(typeof(RegionType)))
			{
				RegionType curRegion = (RegionType)region;
				if (curRegion == RegionType.None)
					continue;

				var borders = GetBorders(curRegion);
				if (!doc.CurrentSettings.OrderingVars[curRegion].Contains(1))
				{
					for (int i = 0; i < distribution.Count; i++)
					{
						var num = distribution[i].number;

						if (distribution[i].number.IsBetweenInclusive(borders.Item1, borders.Item2))
							distribution[i] = (num, new List<(char residue, int count)>());

					}
				}

				if (!doc.CurrentSettings.OrderingVars[curRegion].Contains(2))
				{
					for (int i = 0; i < templateDistribution.Count; i++)
					{
						var num = templateDistribution[i].number;

						if (templateDistribution[i].number.IsBetweenInclusive(borders.Item1, borders.Item2))
							templateDistribution[i] = (num, new List<(char residue, int count)>());

					}
				}
			}


			Dictionary<string, List<(double number, List<(char residue, int count)> frequencies)>> consensusPerSequence = new Dictionary<string, List<(double number, List<(char residue, int count)> frequencies)>>(contigs.Length);
			var readArray = doc.NumberedReads.Select(x => x.read).ToArray();
			//foreach (var contig in contigs)
			Parallel.ForEach(contigs, contig =>
			{
				List<(double number, List<(char residue, int count)> frequencies)> consensus = distribution;
				if (doc.CurrentSettings.RenumberAllFullLengthContigs)
				{
					var containsSeq = false;
					var key = contig.Item1.Sequence.Replace('I', 'L');
					lock (consensusPerSequence)
					{
						containsSeq = consensusPerSequence.TryGetValue(key, out consensus);
					}
					if (!containsSeq)
					{
						var numbering = doc.ReadFilter.GetNumberingAndAlignmentForReadsDynamicProgramming(readArray, new Peptide(contig.Item1.Sequence)).Select(x => (x.read, x.numbering)).ToList();
						consensus = ReadFilter.GetConsensusFromReadsOriginal(numbering).Where(x => x.frequencies.Count != 0).ToList();
						lock (consensusPerSequence)
						{
							if (!consensusPerSequence.ContainsKey(key))
								consensusPerSequence.Add(key, consensus);
						}
					}
				}

				RankedContig con = new RankedContig();
				con.contig = contig.Item1;
				con.numbering = contig.Item2;
				con.conservedness = ExtenderBase<int>.Scoring.GetConservednessSupportScore((contig.Item1.Sequence, contig.Item2), csvPath);
				con.peaks = ExtenderBase<int>.Scoring.GetPEAKSSupportScore((contig.Item1.Sequence, contig.Item2), consensus);
				con.template = ExtenderBase<int>.Scoring.GetTemplateSupport((contig.Item1.Sequence, contig.Item2), templateDistribution).Sum();

				var asm = new AnnotatedSpectrumMatch(doc.Spectrum, contig.Item1, doc.ScoringModel);
				con.spectrum = ExtenderBase<int>.Scoring.GetSpectralSupportScore(asm);
				con.origin = source;

				res.Add(con);
			}
			);

            RankedContig[] resArray = res.ToArray();
            if (doc.CurrentSettings.UseMultiScore)
                resArray = GetMultiScore(doc, null, res);
                

			return Document.Reorder(resArray, new int[] { 3, 5 });
		}

		public static RankedContig[] GetMultiScore(Document doc, int[] multiScoreRange, IEnumerable<RankedContig> res)
		{
			if (!res.Any())
			{
				return res.ToArray();
			}

			var contigsWithIndices = res.Select((x, i) => (x, i)).ToArray();

			multiScoreRange = multiScoreRange ?? new int[] { 0, res.Select(x => x.contig.Length).Max() };

			var range = ((int)multiScoreRange[0], (int)multiScoreRange[1]);
            var numberedReads = doc.NumberedReads.Select(x => (x.read.Peptide, x.numbering)).ToList();
            var length = range.Item2 - range.Item1;
			(double min, double max)[] imgt = res.Select(x =>
			{
				var numbers = x.numbering;
				if (length <= x.contig.Sequence.Length)
					numbers = x.numbering.Skip(range.Item1).Take(length).ToArray();
				return (numbers.Min(), numbers.Max());
			}).ToArray();

			// highest min and lowest max: guarantee all entries have the same endingres
			var minImgt = imgt.Select(x => x.min).Max();
			var maxImgt = imgt.Select(x => x.max).Min();

			var readArray2 = doc.NumberedReads.Select(x => x.read).ToArray();
			Dictionary<int, int> scores = new Dictionary<int, int>();

			if (res.Count() > 1000)
			{
				DialogResult dialogResult = MessageBox.Show($"Do you want to compute the multiscore for these {res.Count()} entries? This will take ~2 minutes per 1000 entries", "large number of contigs detected", MessageBoxButtons.YesNo);
				if (dialogResult == DialogResult.Yes)
				{
				}
				else
				{
					return res.ToArray();
				}
			}

			ConcurrentBag<string> keyBag = new ConcurrentBag<string>();

			Parallel.ForEach(contigsWithIndices, item =>
			{
				var innerContig = item.x;
				var template = new RankedContig()
				{
					contig = new Peptide(),
					numbering = new double[0],
				};

				var seq = innerContig.contig.Sequence;
				var numbers = innerContig.numbering;
				int minIdx = numbers.IndexOf(minImgt);
				int maxIdx = numbers.IndexOf(maxImgt);
				int innerLength = maxIdx - minIdx + 1;

				if (length <= innerContig.contig.Sequence.Length)
				{
					seq = innerContig.contig.Sequence.Substring(minIdx, innerLength);
					numbers = innerContig.numbering.Skip(minIdx).Take(innerLength).ToArray();
				}

				//TODO quick fix, should be if the read is a full length sequence (180 is approximate length of  is approximate length of creg + fr3)
				if (innerContig.contig.Sequence.Length < 200)
				{
					StringBuilder sb = new StringBuilder();
					var nb = new List<double>();
					var mb = new List<Modification>();
					var currentpred = doc.CurrentPrediction.Length < 200 ? doc.NumberedTemplates.First() : (doc.CurrentPrediction, doc.CurrentPredictionNumbering);
					bool added = false;
					if (innerContig.numbering.First() < currentpred.Item2.First())
					{
						sb.Append(innerContig.contig.Sequence);
						nb.AddRange(innerContig.numbering);
						mb.AddRange(innerContig.contig.Modifications);
						added = true;
					}
					for (int i = 0; i < currentpred.Item2.Length; i++)
					{
						var currentNum = currentpred.Item2[i];
						var currentRes = currentpred.Item1.Sequence[i];
						var currentMod = currentpred.Item1.Modifications[i];
						if (currentNum < innerContig.numbering.First())
						{
							sb.Append(currentRes);
							nb.Add(currentNum);
							mb.Add(currentMod);
						}
						else if (currentNum > innerContig.numbering.Last())
						{
							sb.Append(currentRes);
							nb.Add(currentNum);
							mb.Add(currentMod);
						}
						else if (!added)
						{
							sb.Append(innerContig.contig.Sequence);
							nb.AddRange(innerContig.numbering);
							mb.AddRange(innerContig.contig.Modifications);
							added = true;
						}
					}

					template.contig.Sequence = sb.ToString();
					template.numbering = nb.ToArray();
					template.contig.Modifications = mb.ToArray();
				}
				else
				{
					template = new RankedContig()
					{
						contig = innerContig.contig,
						numbering = innerContig.numbering,
					};
				}

				var containsKey = false;
				var key = seq.Replace('I', 'L')
				//.Replace("Q", "K")
				;

				keyBag.Add(key);

				lock (doc.MultiScores)
				{
					if (doc.MultiScores.ContainsKey(key))
					{
						lock (scores)
						{
							if (!scores.ContainsKey(item.i))
							{
								scores.Add(item.i, Coverage.ScoreSequence(doc.MultiScores[key]));
							}
						}
						return;
					}
				}

                
                var innerNumberedReads = doc.CurrentSettings.RenumberAllFullLengthContigs ? doc.ReadFilter.GetNumberingAndAlignmentForReadsDynamicProgramming(readArray2, template.contig).Select(x => (x.read.Peptide, x.numbering)).ToList() : numberedReads; 
                
				var score = FabLabForm.GetMultiSupportScore((key, numbers), innerNumberedReads.ToArray(), tolerance: 7);

				lock (doc.MultiScores)
				{
					if (!doc.MultiScores.ContainsKey(key))
						doc.MultiScores.Add(key, score);

					lock (scores)
					{
						if (!scores.ContainsKey(item.i))
						{
							scores.Add(item.i, Coverage.ScoreSequence(doc.MultiScores[key]));
						}
					}
				}

			});
			if (doc.CurrentSettings.ClipIdentical)
			{
                ClipIdentical(doc, keyBag);
            }

            var resArray = res.ToArray();
			foreach (var item in scores)
			{
				resArray[item.Key].multi = item.Value;
			}

			return resArray;
		}

		private static void ClipIdentical(Document doc, ConcurrentBag<string> keyBag)
		{

			//select start and end
			int startIdx = 0;
			var coverages = keyBag.Distinct().Select(x => doc.MultiScores[x]).ToArray();
			int max = coverages.Select(x => x.Count).Min();

			for (int j = 0; j < max; j++)
			{
				var prev = coverages.Select(x => x[j]);
				var arrays = prev.Select(x => x.Select(y => (y.C, y.N)).ToArray()).ToArray();
				bool isBreak = false;
				for (int i = 1; i < arrays.Length; i++)
				{
					if (!ArraysA.EqualsArray(arrays[i - 1], arrays[i]))
					{
						isBreak = true;
						break;
					}
				}
				if (isBreak)
				{
					startIdx = j;
					break;
				}
			}

			int endSkip = 0;
			for (int j = 0; j < max; j++)
			{
				var prev = coverages.Select(x => x[x.Count - j - 1]);
				var arrays = prev.Select(x => x.Select(y => (y.C, y.N)).ToArray()).ToArray();
				bool isBreak = false;
				for (int i = 1; i < arrays.Length; i++)
				{
					if (!ArraysA.EqualsArray(arrays[i - 1], arrays[i]))
					{
						isBreak = true;
						break;
					}
				}
				if (isBreak)
				{
					endSkip = j;
					break;
				}
			}

			if (startIdx != endSkip && startIdx != 0)
			{
				foreach (var key in keyBag)
				{
					doc.MultiScores[key] = doc.MultiScores[key].Skip(startIdx).Take((doc.MultiScores[key].Count - endSkip) - startIdx).ToList();
				}
			}
		}

		/// <summary>
		/// We account for the I/L and K/Q inaccuracies here!
		/// </summary>
		/// <param name="deNovoReadsEnumerable"></param>
		/// <param name="tagsEnumerable"></param>
		/// <param name="reference"></param>
		/// <param name="precursor"></param>
		/// <returns></returns>
		public static (((Peptide tag, double nDiff) first, (Peptide tag, double nDiff) second, double) tagsWithGap, List<(PeaksPeptideData pep, Peptide, double)> gapFillingPeptides, List<(PeaksPeptideData pep, Peptide, double)> leftExtensions, List<(PeaksPeptideData pep, Peptide, double)> rightExtensions)[] SelectCandidateFillers(IEnumerable<PeaksPeptideData> deNovoReadsEnumerable, IEnumerable<Peptide> tagsEnumerable, Peptide reference, PrecursorInfo precursor)
        {
            var deNovoReads = deNovoReadsEnumerable.ToList();
            List<(Peptide tag, double nDiff)> tagsAndOffset = new List<(Peptide tag, double nDiff)>();

            // we simplify the sequence, to account for read errors
            foreach (var tag in tagsEnumerable)
            {
                try
                {
                    var t = (Peptide)tag.Clone();
                    t.Sequence = t.Sequence.Replace("I", "L");
                    tagsAndOffset.Add((t, t.GetTagOffset(precursor, reference).nDiff));
                }
                catch (Exception)
                {
                }
            }

            // for every tag, we need to find the adjacent tags.
            List<((Peptide tag, double nDiff) first, (Peptide tag, double nDiff) second)> tagsWithNext = tagsAndOffset.SelectMany(inputTag =>
            {
                var endOfTag = inputTag.tag.MonoIsotopicMass - inputTag.tag.Cterm.Delta - MassSpectrometry.MassWater;
                var nonOverlappingRhs = tagsAndOffset.Where(otherTag =>
                {
                    return otherTag.tag.Nterm.Delta > endOfTag;
                });

                return nonOverlappingRhs.Select(x => (inputTag, x));
            }).ToList();

            var ans = tagsWithNext.GroupBy(x => x.first.tag.Sequence + x.second.tag.Sequence).Select(x => x.First());

            var median = Statistics.Median(tagsAndOffset.Select(x => x.nDiff).ToArray());

            List<((Peptide tag, double nDiff) first, (Peptide tag, double nDiff) second, double)> tagsWithGaps = ans
                .Select(x => (x.first, x.second, x.second.tag.Nterm.Delta - (x.first.tag.Sequence.Select(x1 => AminoAcid.Get(x1).MonoIsotopicWeight).Sum() + x.first.tag.Nterm.Delta))).ToList();

            (((Peptide tag, double nDiff) first, (Peptide tag, double nDiff) second, double) tagsWithGap,
                List<(PeaksPeptideData pep, Peptide, double)> gapFillingPeptides,
                List<(PeaksPeptideData pep, Peptide, double)> leftExtensions,
                List<(PeaksPeptideData pep, Peptide, double)> rightExtensions)[] gapFillers = tagsWithGaps.Select(tagsWithGap =>
                {
                    var first = tagsWithGap.first;
                    var second = tagsWithGap.second;
                    double diff = tagsWithGap.Item3;

                    string firstSeq = first.tag.Sequence.Substring(first.tag.Sequence.Length - 3)//.Replace("Q", "K")
                    ;
                    string secondSeq = second.tag.Sequence.Substring(0, 3)
                    //.Replace("Q", "K")
                    ;

                    string firstSeqOg = first.tag.Sequence.Substring(first.tag.Sequence.Length - 3);
                    string secondSeqOg = second.tag.Sequence.Substring(0, 3);

                    List<PeaksPeptideData> containsBoth = new List<PeaksPeptideData>();
                    List<PeaksPeptideData> containsFirst = new List<PeaksPeptideData>();
                    List<PeaksPeptideData> containsSecond = new List<PeaksPeptideData>();

                    // everything is simplified here
                    foreach (var read in deNovoReads)
                    {
                        bool readContainsFirst = read.Peptide.Contains(firstSeq);
                        bool readContainsSecond = read.Peptide.Contains(secondSeq);
                        if (readContainsFirst && readContainsSecond)
                            containsBoth.Add(read);
                        else if (readContainsFirst)
                            containsFirst.Add(read);
                        else if (readContainsSecond)
                            containsSecond.Add(read);
                    }

                    // check if the masses match up to the diff. Because we want the originalSequence Out, we have to find do some extra work...
                    var middles = containsBoth.Select(pep =>
                    {
                        string innerSeq;
                        if (pep.OriginalSequence.Contains(firstSeqOg) && pep.OriginalSequence.Contains(secondSeqOg))
                            innerSeq = Regex.Replace(Regex.Replace(pep.OriginalSequence, $@".*{firstSeqOg}", ""), $@"{secondSeqOg}.*", "");
                        else
                        {
                            int idxStart = pep.Peptide.IndexOf(firstSeq) + 3;
                            int idxEnd = pep.Peptide.IndexOf(secondSeq);
                            int length = idxEnd - idxStart;
                            if (pep.OriginalSequence.Length != pep.Peptide.Length)
                            {
                                throw new NotImplementedException("seqs should be the same length, or errors introduced.");
                            }
                            innerSeq = pep.OriginalSequence.Substring(idxStart, length);
                        }
                         
                        return (pep, new Peptide(innerSeq, pep.Peptide));
                    });
                    var gapFillingPeptides = middles.Select(y => (y.pep, y.Item2, (y.Item2.MonoIsotopicMass - MassSpectrometry.MassWater) - diff)).OrderBy(x => Math.Abs(x.Item3)).ToList();

                    // check if there are peptides that  extend far enough to cover the entire gap, and for those we check the diff between the overlapping peptides
                    //TODO optional intermediate step: check if a part of the sequence of the other peptide is included. lets see if this works first
                    var rightExtensions = RightExtension(firstSeq, firstSeqOg, diff, containsFirst);
                    var leftExtensions = LeftExtension(secondSeq, secondSeqOg, diff, containsSecond);

                    return (tagsWithGap, gapFillingPeptides, leftExtensions, rightExtensions);
                }).ToArray();

            List<(PeaksPeptideData pep, Peptide extension, double offset)> RightExtension(string nTerminalTagSequence, string nTerminalTagSequenceOg, double diff, IEnumerable<PeaksPeptideData> nTerminalPeptidesEnumerable)
            {
                var nTerminalPeptides = nTerminalPeptidesEnumerable.ToList();
                var rightExtensions = nTerminalPeptides.Select(pep =>
                {
                    string innerSeq;
                    if (pep.OriginalSequence.Contains(nTerminalTagSequenceOg))
                        innerSeq = Regex.Replace(pep.OriginalSequence, $@".*{nTerminalTagSequenceOg}", "");
                    else
                    {
                        int idxStart = pep.Peptide.IndexOf(nTerminalTagSequence) + 3;
                        if (pep.OriginalSequence.Length != pep.Peptide.Length)
                        {
                            throw new NotImplementedException("seqs should be the same length, or errors introduced.");
                        }
                        innerSeq = pep.OriginalSequence.Substring(idxStart);
                    }
                    return ExtendWithPeaksPeptide(diff, pep, innerSeq);
                }).OrderBy(x => Math.Abs(x.currDiff)).ToList();

                return rightExtensions;
            }

            List<(PeaksPeptideData pep, Peptide extension, double offset)> LeftExtension(string cTerminalTagSequence, string cTerminalTagSequenceOg, double diff, IEnumerable<PeaksPeptideData> cTerminalPeptidesEnumerable)
            {
                var cTerminalPeptides = cTerminalPeptidesEnumerable.ToList();
                var leftExtensions = cTerminalPeptides.Select(pep =>
                {
                    string innerSeq;
                    if (pep.OriginalSequence.Contains(cTerminalTagSequenceOg))
                        innerSeq = Regex.Replace(pep.OriginalSequence, $@"{cTerminalTagSequenceOg}.*", "");
                    else
                    {
                        int idxEnd = pep.Peptide.IndexOf(cTerminalTagSequence) + 3;
                        if (pep.OriginalSequence.Length != pep.Peptide.Length)
                        {
                            throw new NotImplementedException("seqs should be the same length, or errors introduced.");
                        }
                        innerSeq = pep.OriginalSequence.Substring(0, idxEnd);
                    }
                    // needs to be reversed for the loop
                    innerSeq = innerSeq.ReverseA();
                    var ans1 = ExtendWithPeaksPeptide(diff, pep, innerSeq);
                    ans1.Item2.Sequence = ans1.Item2.Sequence.ReverseA();
                    return ans1;
                }).OrderBy(x => Math.Abs(x.currDiff)).ToList();

                return leftExtensions;
            }

            (PeaksPeptideData pep, Peptide, double currDiff) ExtendWithPeaksPeptide(double diff, PeaksPeptideData pep, string innerSeq)
            {
                StringBuilder extensionBuilder = new StringBuilder();
                double mass = 0;
                double currDiff = int.MaxValue;
                foreach (char aa in innerSeq)
                {
                    //check if the mass of the next extension makes the diff smaller
                    mass += HeckLib.chemistry.AminoAcid.Get(aa).MonoIsotopicWeight;
                    double nextDiff = mass - diff;
                    if (Math.Abs(nextDiff) > Math.Abs(currDiff))
                        break;
                    // if so, extend it, and set the currentDiff
                    extensionBuilder.Append(aa);
                    currDiff = nextDiff;
                }
                return (pep, new Peptide(extensionBuilder.ToString(), pep.Peptide), currDiff);
            }

            return gapFillers;
        }

        public static List<Peptide> FillGapsWithCandidates((((Peptide tag, double nDiff) first, (Peptide tag, double nDiff) second, double) tagsWithGap, List<(PeaksPeptideData pep, Peptide, double)> gapFillingPeptides, List<(PeaksPeptideData pep, Peptide, double)> leftExtensions, List<(PeaksPeptideData pep, Peptide, double)> rightExtensions)[] gapfillers, Tolerance innerTol, SpectrumContainer spectrum)
		{
            // make the first peptide
            var outList = new List<Peptide>();

            foreach (var item in gapfillers)
			{
                (Peptide tag, double nDiff) first = item.tagsWithGap.first;
                (Peptide tag, double nDiff) second = item.tagsWithGap.second;

                string firstSeq = first.tag.Sequence.Substring(first.tag.Sequence.Length - 3)
                    //.Replace("Q", "K")
                    .Replace("I", "L");
                string secondSeq = second.tag.Sequence.Substring(0, 3)
                    //.Replace("Q", "K")
                    .Replace("I", "L");

                string firstSeqOg = first.tag.Sequence.Substring(first.tag.Sequence.Length - 3);
                string secondSeqOg = second.tag.Sequence.Substring(0, 3);

                double diff = item.tagsWithGap.Item3;

                var matchingReads = item.gapFillingPeptides;
                var uniqueMatchingReads = matchingReads.GroupBy(x => x.Item2.Sequence).Select(x => x.First()).ToList();
                var toLeftExtensions = item.leftExtensions;
                var toRightExtensions = item.rightExtensions;
                var rhsExtensions = toLeftExtensions.GroupBy(x => x.Item2.Sequence).Select(x => x.First()).ToList();
                var lhsExtensions = toRightExtensions.GroupBy(x => x.Item2.Sequence).Select(x => x.First()).ToList();

                // First we check to see if there are gapfillers that fill the gap within 0.5 Da
                var ans = FindMassMatchingGapFillers(innerTol, first, second, diff, uniqueMatchingReads).Select(x => new Peptide(x.filler)).ToList();

                // we also check if any peptides were gapfillers despite not including one of the flanks
                ans.AddRange(FindMassMatchingGapFillers(innerTol, first, second, diff, rhsExtensions).Select(x => new Peptide(x.filler)).ToList());
                ans.AddRange(FindMassMatchingGapFillers(innerTol, first, second, diff, lhsExtensions).Select(x => new Peptide(x.filler)).ToList());
				
                // gettwopartgapfiller returns a dictionary with the evidence for the rhs, and the extensions. so we have to recombine. This may be pretty excessive...
                ans.AddRange(GetTwoPartGapFillers(innerTol, first, second, diff, lhsExtensions, rhsExtensions).SelectMany(lhsAndExtensions => lhsAndExtensions.Value.Select(combo => new Peptide(lhsAndExtensions.Key + combo.rhsExtension))).GroupBy(x => x.Sequence).Select(x => x.First()));

                outList.AddRange(ans.GroupBy(x => x.Sequence).Select(x => x.First()).Select(x =>
                {
                    // the total should be the same as waterless, monoisotopic precursor

                    var precursorMass = HeckLib.chemistry.Proteomics.ConvertAverageMassToMonoIsotopic(spectrum.PrecursorMass, true) - MassSpectrometry.MassWater;

                    var n = Modification.CreateOffsetMod(0, first.tag.Nterm.Delta + first.tag.Sequence.Select(aa => AminoAcid.Get(aa).MonoIsotopicWeight).Sum(), Modification.PositionType.anyNterm, Modification.TerminusType.nterm);
                    var c = Modification.CreateOffsetMod(0, precursorMass - (first.tag.Nterm.Delta + first.tag.Sequence.Select(aa => AminoAcid.Get(aa).MonoIsotopicWeight).Sum() + x.Sequence.Select(aa => AminoAcid.Get(aa).MonoIsotopicWeight).Sum()), Modification.PositionType.anyCterm, Modification.TerminusType.cterm);
                    return new Peptide(x)
                    {
                        Nterm = n,
                        Cterm = c,
                    };
                }));
            }

            return outList;

            (string lhs, string rhs, double diff) ExtendWithPeaksPeptide(double diff, string lhs, string rhs)
			{
				StringBuilder extensionBuilder = new StringBuilder();
				double massExtension = 0;
				double currDiff = diff;
                // we extend towards the middle, so we have to reverse because the RHS is written left to right, and the first res we take is the leftmost
				var reverseRhs = rhs.Reverse();
				foreach (char aa in reverseRhs)
				{
					//check if the mass of the next extension makes the diff lower than 0
                    // if abs nextdiff is rising, we have gone below zero and further away. if we go below zero but by less than the current diff, we accept
					massExtension += HeckLib.chemistry.AminoAcid.Get(aa).MonoIsotopicWeight;
					double nextDiff = diff - massExtension;
					if (Math.Abs(nextDiff) > Math.Abs(currDiff))
						break;
					// if so, extend it, and set the currentDiff
					extensionBuilder.Append(aa);
					currDiff = nextDiff;
				}
				var extension = extensionBuilder.ToString().Reverse();

				return (lhs, extension, currDiff);
			}

            Dictionary<string, List<(string lhs, string rhsExtension, double remainingDiff)>> GetTwoPartGapFillers(Tolerance innerTol2, (Peptide tag, double nDiff) first, (Peptide tag, double nDiff) second, double diff, List<(PeaksPeptideData pep, Peptide extension, double)> leftExtensions, List<(PeaksPeptideData pep, Peptide extension, double)> rightExtensions)
			{
				// the gap can't be filled with a single peptide
				// we will look for combinations that can fill the gap.
				// K/Q is handled earlier so all of this works

				// all extensions from the 
				List<((PeaksPeptideData pep, Peptide, double) lhs, IEnumerable<(PeaksPeptideData pep, Peptide, double)> extensions)> combos = leftExtensions.Select(lhs => (lhs, rightExtensions.Where(rhs =>
				{
					double rightMass = (rhs.extension.MonoIsotopicMass - MassSpectrometry.MassWater);
					double leftMass = (lhs.extension.MonoIsotopicMass - MassSpectrometry.MassWater);
                    // this will only work if the innertol is given as an absolute value (which it should be)
					return leftMass + rightMass >= diff - innerTol2.Value;
				}))).ToList();

                // we save the extensions here
				var lhsSequencesAndTheirExtensions = new Dictionary<string, List<(string lhs, string rhsExtension, double remainingDiff)>>();
				foreach (var combo in combos)
				{
					(PeaksPeptideData pep, Peptide extension, double) lhs = combo.lhs;
					// loop over the sequence, first taking the whole sequence, then minus one aa ;
					for (int i = 1; i <= lhs.extension.Length; i++)
					{
                        // the left hand part of the sequence is shortened by 1 residue every loop
						string lhsInnerSeq = lhs.Item2.Sequence.Substring(0, i);

						var leftMass = lhsInnerSeq.Select(aa => AminoAcid.Get(aa).MonoIsotopicWeight).Sum();
						var remainingDiff = diff - leftMass;

						if (remainingDiff < AminoAcid.Get('G').MonoIsotopicWeight)
							continue;

						// and check if we have peaks peptides that can extend to fill the void
						// is there already a list of peptides for this key? if so it should contain the same results, so we skip
						if (!lhsSequencesAndTheirExtensions.ContainsKey(lhsInnerSeq))
						{
							List<(string lhs, string rhs, double diff)> extensions = combo.extensions.Select(rhs =>
                            {
                                return ExtendWithPeaksPeptide(remainingDiff, lhsInnerSeq, rhs.Item2.Sequence);
                            }).ToList();
							var suitableExtensions = extensions.Where(extended => innerTol2.IsDuplicate(remainingDiff, extended.rhs.Select(aa => AminoAcid.Get(aa).MonoIsotopicWeight).Sum()));
							if (suitableExtensions.Any())
							{
								lhsSequencesAndTheirExtensions.Add(lhsInnerSeq, suitableExtensions.ToList());
							}
						}
					}
				}

				return lhsSequencesAndTheirExtensions;
			}
		}

        private static (string filler, IEnumerable<(PeaksPeptideData pep, Peptide, double diff)>)[] FindMassMatchingGapFillers(Tolerance innerTol, (Peptide tag, double nDiff) first, (Peptide tag, double nDiff) second, double diff, List<(PeaksPeptideData pep, Peptide, double)> matchingReads)
		{
			IEnumerable<(double, (PeaksPeptideData pep, Peptide, double) x)> diffs = matchingReads.Select(x => (x.Item2.MonoIsotopicMass - MassSpectrometry.MassWater, x)).Where(x => x.x.Item2.Sequence != string.Empty);

			// check for matching masses and off by one errors
			// TODO maybe we should slide our extended tags to a best fitting position
			var matched = diffs.Where(x => innerTol.IsDuplicate(x.Item1, diff)).Select(x => x.x).GroupBy(x => x.Item2.Sequence).Select(x => (x.Key, matchingReads.Where(y => x.Key.Contains(y.Item2.Sequence)))).OrderByDescending(x => x.Item2.Count());

			(string filler, IEnumerable<(PeaksPeptideData pep, Peptide, double diff)>)[] ps = matched.ToArray();
			return ps;
        }

        /// <summary>
        /// order the numbers to match imgt style numbering. 
        /// only works until 112.9 and 111.9!
        /// </summary>
        /// <param name="toSort"></param>
        /// <returns></returns>
        public static double[] OrderImgtNumbering(IEnumerable<double> toSort)
        {
            var sorted = toSort.Distinct().OrderBy(x => x).ToArray();
            
            var containsNums = SortedPerInput.TryGetValue(sorted, out List<double> toReturn);
            
            if (!containsNums)
            {
                toReturn = new List<double>();
                toReturn.AddRange(sorted.Where(x => x <= 111).OrderBy(x => x));
                
                toReturn.AddRange(sorted.Where(x => x.IsBetweenExclusive(111, 112)).OrderBy(x => x));
                toReturn.AddRange(sorted.Where(x => x.IsBetweenExclusive(112, 113)).OrderByDescending(x => x));
                toReturn.AddRange(sorted.Where(x => x >= 113 || x == 112).OrderBy(x => x));

                if (!SortedPerInput.ContainsKey(sorted))
                    SortedPerInput.Add(sorted, toReturn);
            }

            return toReturn.ToArray();
        }

        /// <summary>
        /// Get the borders of this region, i.e. first and last imngt number
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static (double, double) GetBorders(RegionType type)
        {
            switch (type)
            {
                case RegionType.FR1:
                    return (double.MinValue, 26);
                case RegionType.CDR1:
                    return (27, 38);
                case RegionType.FR2:
                    return (39, 55);
                case RegionType.CDR2:
                    return (56, 65);
                case RegionType.FR3:
                    return (66, 104);
                case RegionType.CDR3:
                    return (105, 117);
                case RegionType.FR4:
                    return (118, double.MaxValue);
                default:
                    throw new System.Exception();
            }
        }

        public static ImgtFilterLib.LociEnum GetLocusEnum(string name)
        {
            string chainName = GetChainName(name);

            if (chainName == "H")
            {
                return ImgtFilterLib.LociEnum.Heavy;
            }
            else if (chainName == "L")
            {
                return ImgtFilterLib.LociEnum.Lambda;
            }
            else if (chainName == "K")
            {
                return ImgtFilterLib.LociEnum.Kappa;
            }
            else 
                return ImgtFilterLib.LociEnum.TBD;
        }

        /// <summary>
        /// not super elegant but should do the trick. Set any gap in cdr to the middle.
        /// </summary>
        /// <param name="region"></param>
        /// <param name="sequence"></param>
        /// <returns></returns>
        public static double[] NumberCdr(RegionType region, string sequence)
        {
			switch (region)
			{
				case RegionType.FR1:
                    return ImgtNumberer.NumberCdr(PsrmLib.Processing.RegionType.FR1, sequence);
					break;
				case RegionType.CDR1:
                    return ImgtNumberer.NumberCdr(PsrmLib.Processing.RegionType.CDR1, sequence);
                    break;
				case RegionType.FR2:
                    return ImgtNumberer.NumberCdr(PsrmLib.Processing.RegionType.FR2, sequence);
                    break;
				case RegionType.CDR2:
                    return ImgtNumberer.NumberCdr(PsrmLib.Processing.RegionType.CDR2, sequence);
                    break;
				case RegionType.FR3:
                    return ImgtNumberer.NumberCdr(PsrmLib.Processing.RegionType.FR3, sequence);
                    break;
				case RegionType.CDR3:
                    return ImgtNumberer.NumberCdr(PsrmLib.Processing.RegionType.CDR3, sequence);
                    break;
				case RegionType.FR4:
                    return ImgtNumberer.NumberCdr(PsrmLib.Processing.RegionType.FR4, sequence);
                    break;
				case RegionType.None:
             
					break;
				default:
					break;
			}
            throw new Exception("No region provided");
        }

        public static string GetChainName(string name)
        {
            return Regex.Replace(Regex.Replace(name, ".*_IG", ""), "[CVDJG].*\\*[0-9]+.*", "");
        }

        public static string GetSequence(string seq, double[] innerNumbering, ImgtFilterLib.Enums.RegionEnum type, ImgtFilterLib.LociEnum chain, out double[] numbering)
        {
            (double, double) borders = GetBorders(type, chain);
            double startImgt = borders.Item1;
            double endImgt = borders.Item2;

            int startIdx = innerNumbering.Where(x => x >= startImgt).Any() ? innerNumbering.IndexOf(innerNumbering.Where(x => x >= startImgt).First()) : -1;
            numbering = innerNumbering.Where(x => x.IsBetweenInclusive(startImgt - 0.01, endImgt + 0.01)).ToArray();
            //endIdx = numbering.IndexOf(numbering.Where(x => x <= endImgt).Last());
            int length = numbering.Count();

            // would throw errors
            if (startIdx == -1)
            {
                numbering = new double[0];
                return "";
            }
            if (startIdx + length > seq.Length)
            {
                numbering = new double[0];
                return "";
            }

            return seq.Substring(startIdx, length);
        }

        /// <summary>
        /// Get the borders of this region, i.e. first and last imngt number
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static (double, double) GetBorders(ImgtFilterLib.Enums.RegionEnum type, ImgtFilterLib.Enums.AbDataBaseEnum chain)
        {
            switch (chain)
            {
                case ImgtFilterLib.Enums.AbDataBaseEnum.HeavyChainSequences:
                    return GetBorders(type, ImgtFilterLib.Enums.AbType.Heavy);
                case ImgtFilterLib.Enums.AbDataBaseEnum.LightChainSequencesKappa:
                    return GetBorders(type, ImgtFilterLib.Enums.AbType.Light);
                case ImgtFilterLib.Enums.AbDataBaseEnum.LightChainSequencesLambda:
                    return GetBorders(type, ImgtFilterLib.Enums.AbType.Light);
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Get the borders of this region, i.e. first and last imngt number
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static (double, double) GetBorders(ImgtFilterLib.Enums.RegionEnum type, ImgtFilterLib.LociEnum chain)
        {
            switch (chain)
            {
                case ImgtFilterLib.LociEnum.Kappa:
                    return GetBorders(type, ImgtFilterLib.Enums.AbType.Light);
                case ImgtFilterLib.LociEnum.Lambda:
                    return GetBorders(type, ImgtFilterLib.Enums.AbType.Light);
                case ImgtFilterLib.LociEnum.Heavy:
                    return GetBorders(type, ImgtFilterLib.Enums.AbType.Heavy);
                case ImgtFilterLib.LociEnum.TBD:
                    throw new NotImplementedException();
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Get the borders of this region, i.e. first and last imngt number
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static (double, double) GetBorders(ImgtFilterLib.Enums.RegionEnum type, ImgtFilterLib.Enums.AbType chain)
        {
            switch (type)
            {
                case ImgtFilterLib.Enums.RegionEnum.VRegion:
                    switch (chain)
                    {
                        case ImgtFilterLib.Enums.AbType.Light:
                            return (1, 115);
                        case ImgtFilterLib.Enums.AbType.Heavy:
                            return (1, 106);
                        default:
                            break;
                    }
                    break;
                case ImgtFilterLib.Enums.RegionEnum.DRegion:
                    return (107, 113);
                case ImgtFilterLib.Enums.RegionEnum.JRegion:
                    switch (chain)
                    {
                        case ImgtFilterLib.Enums.AbType.Light:
                            return (116, 128);
                        case ImgtFilterLib.Enums.AbType.Heavy:
                            return (114, 128);
                        default:
                            break;
                    }
                    break;
                case ImgtFilterLib.Enums.RegionEnum.CRegion:
                    return (129, double.MaxValue);
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
            throw new Exception("weird enum passed");
        }

        /// <summary>
        /// Get the flanking regions
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static (RegionType, RegionType) GetFlanking(RegionType type)
        {
            switch (type)
            {
                case RegionType.FR1:
                    return (RegionType.None, RegionType.CDR1);
                case RegionType.CDR1:
                    return (RegionType.FR1, RegionType.FR2);
                case RegionType.FR2:
                    return (RegionType.CDR1, RegionType.CDR2);
                case RegionType.CDR2:
                    return (RegionType.FR2, RegionType.FR3);
                case RegionType.FR3:
                    return (RegionType.CDR2, RegionType.CDR3);
                case RegionType.CDR3:
                    return (RegionType.FR3, RegionType.FR4);
                case RegionType.FR4:
                    return (RegionType.CDR3, RegionType.None);
                default:
                    throw new System.Exception();
            }
        }

        public static bool IsCdr(RegionType region)
        {
            switch (region)
            {
                case RegionType.FR1:
                    return false;
                case RegionType.CDR1:
                    return true;
                case RegionType.FR2:
                    return false;
                case RegionType.CDR2:
                    return true;
                case RegionType.FR3:
                    return false;
                case RegionType.CDR3:
                    return true;
                case RegionType.FR4:
                    return false;
                default:
                    throw new NotImplementedException("unimplemented enum");
            }
        }

        public static int GetCdrIdx(RegionType region)
        {
            switch (region)
            {
                case RegionType.FR1:
                    return -1;
                case RegionType.CDR1:
                    return 0;
                case RegionType.FR2:
                    return -1;
                case RegionType.CDR2:
                    return 1;
                case RegionType.FR3:
                    return -1;
                case RegionType.CDR3:
                    return 2;
                case RegionType.FR4:
                    return -1;
                default:
                    throw new NotImplementedException("unimplemented enum");
            }
        }
    }
}
