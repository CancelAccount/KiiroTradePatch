using System;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace KiiroTradePatch
{
    /// <summary>
    /// 按 defName 前缀（Kiiro）出售绮罗武器。
    /// 取代原版按全局规则（WeaponRanged tag / WeaponsMelee 类别）出售的 StockGenerator_MarketValue，
    /// 使绮罗定居点只出售绮罗武器、不出现其他种族武器。
    /// 绮罗武器没有统一的 ThingCategoryDef（不像衣物有 Kiiro_Apparel 类别树），因此以 defName 以 Kiiro 开头作为判定约定。
    /// </summary>
    public class StockGenerator_SellKiiroWeapon : StockGenerator_MiscItems
    {
        /// <summary>
        /// 构造器：默认出售 3~6 件（XML 中可通过 countRange 覆盖）。
        /// </summary>
        public StockGenerator_SellKiiroWeapon()
        {
            countRange = new IntRange(3, 6);
        }

        /// <summary>
        /// 匹配条件：属于武器、defName 以 Kiiro 开头、且允许随机生成的武器（generateAllowChance > 0）。
        /// generateAllowChance = 0 的物品（如驻殖民地商人的专卖品）按原设定不生成。
        /// 继承的 GenerateThings 会按该条件挑选武器生成出售库存。
        /// </summary>
        public override bool HandlesThingDef(ThingDef thingDef)
        {
            if (!thingDef.IsWeapon)
            {
                return false;
            }
            // 排除勇者佩剑（Kiiro_ValorSabre）
            if (thingDef.weaponTags != null && thingDef.weaponTags.Contains("Kiiro_LegendMelee"))
            {
                return false;
            }
            return thingDef.generateAllowChance > 0f
                && thingDef.defName.StartsWith("Kiiro", StringComparison.OrdinalIgnoreCase);
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
