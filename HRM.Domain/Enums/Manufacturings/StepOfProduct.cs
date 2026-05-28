using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Domain.Enums.Manufacturings
{
    public enum StepOfProduct : int
    {
        Forsb = 0,             // Sang bao
        MD_GH = 1,             // Đùn
        CT_GH = 2,             // Trộn
        HT_GH = 3,             // Trộn Recolor
        BN_GH = 4,             // Nghiền
        CT_MD_GH = 5,          // Trộn => Đùn
        CT_MD_HT_GH = 6,       // Trộn => Đùn => Trộn Recolor
        CT_BN_MD_GH = 7,       // Trộn => Nghiền => Đùn
        CT_BN_MD_HT_GH = 8,    // Trộn => Nghiền => Đùn => Trộn Recolor
    }
}
