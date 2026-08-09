using System.Collections.Generic;
using System.Linq;
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
    }
}
