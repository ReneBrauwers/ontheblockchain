using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Extensions
{
    public static class Doubles
    {
        public static string FormatDoubleAsPercentage(this double value)
        {
            //	<td>@voteOption.TotalVotes.ToString("N", System.Globalization.CultureInfo.InvariantCulture)</td>

            return ((double)value * 100).ToString("N2");
        }

        public static string FormatDoubleAsRound(this double value)
        {
            //	<td>@voteOption.TotalVotes.ToString("N", System.Globalization.CultureInfo.InvariantCulture)</td>

            return ((double)value).ToString("N", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string FormatDoubleAsThousands(this double value)
        {
            //	<td>@voteOption.TotalVotes.ToString("N", System.Globalization.CultureInfo.InvariantCulture)</td>
            var inThousands = $"{Convert.ToInt32(((double)value / 1000)).ToString()} K";
            return inThousands;
        }
    }
}
