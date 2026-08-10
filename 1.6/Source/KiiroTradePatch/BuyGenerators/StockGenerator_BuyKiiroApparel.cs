using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace KiiroTradePatch
{
    /// <summary>
    /// 收购「绮罗可装备的绮罗衣物」的交易收购器。
    /// 匹配规则：
    ///   1. 物品是衣物，且其 thingCategories 递归属于 Kiiro_Apparel（排除所有非绮罗衣物）；
    ///   2. 衣物覆盖的每个 bodyPartGroup 都能在绮罗身体（Kiiro_Body）上找到对应部位（排除绮罗无法装备的衣物）；
    ///   3. 可通过 XML/注入器配置收购范围（dailyOnly / excludeDailyApparel），对齐各商队原版衣物收购意向。
    /// </summary>
    public class StockGenerator_BuyKiiroApparel : StockGenerator
    {
        /// <summary>只收购绮罗日常衣物（排除护甲/太空服/炮台等非日常类），由 XML 按商队配置。</summary>
        public bool dailyOnly;

        /// <summary>只收购绮罗非日常衣物（护甲/太空服/炮台等），由 XML 按商队配置。</summary>
        public bool excludeDailyApparel;

        /// <summary>只收购不售出，因此不生成库存。</summary>
        public override IEnumerable<Thing> GenerateThings(PlanetTile forTile, Faction faction = null)
        {
            return Enumerable.Empty<Thing>();
        }

        /// <summary>是否处理该物品：必须是可以被绮罗装备的绮罗衣物，并按配置的收购范围过滤。</summary>
        public override bool HandlesThingDef(ThingDef thingDef)
        {
            if (thingDef == null || !thingDef.IsApparel)
            {
                return false;
            }
            // 排除勇者装备（Kiiro_ValorArmor / Kiiro_ValorTiara）：
            if (thingDef.apparel != null
                && thingDef.apparel.tags != null
                && thingDef.apparel.tags.Contains("Kiiro_SpecialApparel"))
            {
                return false;
            }
            if (!KiiroApparelHelper.IsKiiroApparel(thingDef)
                || !KiiroApparelHelper.CanKiiroWear(thingDef))
            {
                return false;
            }
            // 按商队原版收购意向配置收购范围（由 XML 或注入器设置）：
            if (dailyOnly)
            {
                // 只收购绮罗日常衣物
                return !KiiroApparelHelper.NotDailyApparel(thingDef);
            }
            if (excludeDailyApparel)
            {
                // 只收购绮罗非日常衣物
                return KiiroApparelHelper.NotDailyApparel(thingDef);
            }
            return true;
        }

        /// <summary>只在物品可交易且符合收购条件时允许玩家售卖给该商人。</summary>
        public override Tradeability TradeabilityFor(ThingDef thingDef)
        {
            if (thingDef.tradeability == Tradeability.None || !HandlesThingDef(thingDef))
            {
                return Tradeability.None;
            }
            return Tradeability.Sellable;
        }
    }
}
