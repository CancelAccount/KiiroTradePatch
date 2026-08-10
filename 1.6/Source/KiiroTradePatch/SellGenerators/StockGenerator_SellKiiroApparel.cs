using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace KiiroTradePatch
{
    /// <summary>
    /// 按 Kiiro_Apparel 类别出售绮罗可装备的绮罗衣物（与收购器 StockGenerator_BuyKiiroApparel 互补）。
    /// 取代原版按全局 tradeTag（BasicClothing/Clothing/Armor）出售的 StockGenerator_MarketValue，
    /// 使绮罗定居点/商队只出售绮罗衣物、不出现人类/鼠族等其他种族衣物。
    /// 可通过 XML/注入器配置出售范围（dailyOnly / excludeDailyApparel），
    /// 与收购器保持一致，避免出售器命中导致 WillTrade 绕过收购范围（玩家仍可卖出被排除的衣物）。
    /// </summary>
    public class StockGenerator_SellKiiroApparel : StockGenerator_MiscItems
    {
        /// <summary>只出售绮罗日常衣物（排除护甲/太空服/作战服装等非日常类），由 XML 按商队配置。</summary>
        public bool dailyOnly;

        /// <summary>只出售绮罗非日常衣物（护甲/太空服/作战服装等），由 XML 按商队配置。</summary>
        public bool excludeDailyApparel;

        /// <summary>
        /// 构造器：默认出售 8~12 件（XML 中可通过 countRange 覆盖）。
        /// </summary>
        public StockGenerator_SellKiiroApparel()
        {
            countRange = new IntRange(8, 12);
        }

        /// <summary>
        /// 匹配条件：属于 Kiiro_Apparel 类别（含扩展子类）、绮罗身体可装备、
        /// 允许随机生成的衣物（generateAllowChance > 0），并按配置的出售范围过滤。
        /// 继承的 GenerateThings 会按该条件挑选衣物生成出售库存。
        /// </summary>
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
            if (thingDef.generateAllowChance <= 0f
                || !KiiroApparelHelper.IsKiiroApparel(thingDef)
                || !KiiroApparelHelper.CanKiiroWear(thingDef))
            {
                return false;
            }
            // 按商队原版出售意向配置出售范围（由 XML 或注入器设置，与收购器保持一致）：
            if (dailyOnly)
            {
                // 只出售绮罗日常衣物（排除护甲/太空服/作战服装等非日常类）
                return !KiiroApparelHelper.NotDailyApparel(thingDef);
            }
            if (excludeDailyApparel)
            {
                // 只出售绮罗非日常衣物（护甲/作战服装等）
                return KiiroApparelHelper.NotDailyApparel(thingDef);
            }
            return true;
        }

        /// <summary>
        /// 选择权重：按市场价衰减（越便宜越常见），与原版 StockGenerator_MarketValue 行为一致。
        /// </summary>
        protected override float SelectionWeight(ThingDef thingDef)
        {
            return SelectionWeightMarketValueCurve.Evaluate(thingDef.BaseMarketValue);
        }

        /// <summary>市场价权重曲线（复制原版 StockGenerator_MarketValue 的私有曲线）。</summary>
        private static readonly SimpleCurve SelectionWeightMarketValueCurve = new SimpleCurve
        {
            { new CurvePoint(0f, 1f), true },
            { new CurvePoint(500f, 0.5f), true },
            { new CurvePoint(1500f, 0.2f), true },
            { new CurvePoint(5000f, 0.1f), true }
        };
    }
}
