using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlantConditionAnalyzer.Core.Models
{
    public static class PlantProfile
    {
        static public int Id { get; set; }
        static public string Name { get; set; } = "BeetRoot";

        //2018.03.08 y = 220,69x + 17,385        ax+b

        static public double SpadSlope { get; set; } = 220.69;   //a
        static public double SpadIntercept { get; set; } = 17.385; // b

        static public string PreferredIndex { get; set; } = "MGRVI";
    }
}
