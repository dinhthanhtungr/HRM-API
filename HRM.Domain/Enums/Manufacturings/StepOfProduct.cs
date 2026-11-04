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
        MDTS_GH = 6,           // Đùn tái sinh
        BTS_GH = 7,            // Băm gia công
        CT_MD_HT_GH = 8,       // Trộn => Đùn => Hoàn thiện
        MD_HT_GH = 9,          // Đùn => Hoàn thiện
        CT_HT_GH = 10,         // Trộn => Hoàn thiện (chia phần)
        CT_MDTS_GH = 11,       // Trộn => Đùn tái sinh
    }
}
