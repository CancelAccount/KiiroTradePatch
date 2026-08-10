using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace KiiroTradePatch
{
    /// <summary>
    /// 绮罗衣物相关的静态辅助与缓存
    /// </summary>
    public static class KiiroApparelHelper
    {
        /// <summary>绮罗衣物所属的 ThingCategoryDef 名称。</summary>
        public const string KiiroApparelCategoryDefName = "Kiiro_Apparel";

        /// <summary>绮罗种族身体 BodyDef 名称。</summary>
        public const string KiiroBodyDefName = "Kiiro_Body";

        /// <summary>解锁绮罗非日常衣物（护甲/太空服/高科技装备）的制造科技 defName 集合。</summary>
        private static readonly HashSet<string> CombatResearchDefNames = new HashSet<string>
        {
            "Kiiro_Smithing",        // 锻造：轻型胸甲等护甲
            "Kiiro_Machining",       // 机加工：护盾腰带等高科技实用装备
            "Kiiro_Apparel_Vacsuit"  // 太空服（气密服/气密头盔）
        };

        /// <summary>Kiiro_Apparel 类别（含其子类别中的所有绮罗衣物扩展）。</summary>
        private static ThingCategoryDef kiiroApparelCategory;

        /// <summary>绮罗身体上全部部位所带有的 BodyPartGroup 集合（用于判断能否装备）。</summary>
        private static HashSet<BodyPartGroupDef> kiiroBodyGroups;

        /// <summary>在 defs 加载完成后初始化缓存（确保 Kiiro_Body / Kiiro_Apparel 可用）。</summary>
        public static void EnsureLoaded()
        {
            if (kiiroApparelCategory != null && kiiroBodyGroups != null)
            {
                return;
            }
            kiiroApparelCategory = DefDatabase<ThingCategoryDef>.GetNamed(KiiroApparelCategoryDefName);
            BodyDef kiiroBody = DefDatabase<BodyDef>.GetNamed(KiiroBodyDefName);
            kiiroBodyGroups = new HashSet<BodyPartGroupDef>(
                kiiroBody.AllParts.SelectMany(part => part.groups));
        }

        /// <summary>判断衣物是否属于绮罗类别：其 thingCategories 任一递归父链上出现 Kiiro_Apparel。</summary>
        public static bool IsKiiroApparel(ThingDef thingDef)
        {
            if (thingDef.thingCategories == null)
            {
                return false;
            }
            for (int i = 0; i < thingDef.thingCategories.Count; i++)
            {
                for (ThingCategoryDef current = thingDef.thingCategories[i]; current != null; current = current.parent)
                {
                    if (current == kiiroApparelCategory)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>判断衣物能否被绮罗装备：衣物覆盖的每个 bodyPartGroup 都必须在绮罗身体上存在对应部位。</summary>
        public static bool CanKiiroWear(ThingDef thingDef)
        {
            if (thingDef.apparel == null || thingDef.apparel.bodyPartGroups == null)
            {
                return false;
            }
            for (int i = 0; i < thingDef.apparel.bodyPartGroups.Count; i++)
            {
                if (!kiiroBodyGroups.Contains(thingDef.apparel.bodyPartGroups[i]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 判断衣物是否为绮罗非日常类（护甲/太空服/高科技装备）：
        /// 满足以下任一条件即为非日常：
        ///   1. 衣物科技等级在工业时代及以后（techLevel >= Industrial，覆盖护盾腰带等高科技实用装备）；
        ///   2. 解锁该衣物的制造科技命中非日常科技（锻造/机加工/太空服）。
        /// 其余（含日常裁缝科技、未知扩展科技或低科技衣物）→ 日常。
        /// 未知科技（如扩展 mod 新增）保守判定为日常，避免把扩展外观服误判为非日常。
        /// </summary>
        public static bool NotDailyApparel(ThingDef thingDef)
        {
            // 工业时代及以后的高科技衣物视为非日常（护盾腰带/烟雾弹腰带等高科实用装备）。
            if (thingDef.techLevel >= TechLevel.Industrial)
            {
                return true;
            }
            if (thingDef.recipeMaker == null)
            {
                return false;
            }
            if (thingDef.recipeMaker.researchPrerequisite != null
                && CombatResearchDefNames.Contains(thingDef.recipeMaker.researchPrerequisite.defName))
            {
                return true;
            }
            if (thingDef.recipeMaker.researchPrerequisites != null)
            {
                for (int i = 0; i < thingDef.recipeMaker.researchPrerequisites.Count; i++)
                {
                    ResearchProjectDef research = thingDef.recipeMaker.researchPrerequisites[i];
                    if (research != null && CombatResearchDefNames.Contains(research.defName))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>判断衣物是否为绮罗日常衣物：属于 Kiiro_Apparel、绮罗可装备、且非护甲/太空服等非日常类。</summary>
        public static bool IsKiiroDailyApparel(ThingDef thingDef)
        {
            return IsKiiroApparel(thingDef) && CanKiiroWear(thingDef) && !NotDailyApparel(thingDef);
        }
    }
}
