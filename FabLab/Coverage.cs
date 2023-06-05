using System.Collections.Generic;
using System.Linq;

namespace FabLab
{
	public class Coverage
	{
		public int Idx;
		public double Imgt;
		public int N;
		public int C;

		public int Sum { get => N + C; }

		public static int ScoreSequence(List<List<Coverage>> coverages)
		{
			var flat = coverages.SelectMany(x => x.Select(y => y)).ToArray();
			return flat.Select(x => x.Sum).Sum();
		}
	}
 
}
