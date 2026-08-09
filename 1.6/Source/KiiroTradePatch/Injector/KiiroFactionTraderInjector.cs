using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace KiiroTradePatch
{
    /// <summary>
    /// 派系扩展兼容：在 defs 加载完成后，对所有「绮罗系派系」（defName 或 categoryTag 以 Kiiro 开头，含第三方派系扩展）生效：
    ///   1. 把其引用的原版共享交易定义替换为绮罗专属交易定义；
    ///   2. 动态克隆隔离：派系扩展若引用了被鼠族注入收购 tag 的共享商队（作战补给商/海盗商/轨道/帝国等），
    ///      自动克隆为绮罗专属副本（defName = Kiiro_ + 原名，翻译见 TraderKinds_KiiroClones.xml）并替换引用；
    ///   3. 对其自有交易定义（defName 以 Kiiro 开头）清理 出售非绮罗物品的出售器，并注入绮罗衣物/武器收购器与出售器。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class KiiroFactionTraderInjector
    {
        static KiiroFactionTraderInjector()
        {
            LongEventHandler.QueueLongEvent(TryApply, "KiiroTradePatch.Inject", false, null);
        }

        /// <summary>入口：在 defs 全部加载（含所有 patch）完成后执行。</summary>
        private static void TryApply()
        {
            try
            {
                KiiroApparelHelper.EnsureLoaded();
                foreach (FactionDef factionDef in DefDatabase<FactionDef>.AllDefs)
                {
                    if (IsKiiroFaction(factionDef))
                    {
                        ApplyToFaction(factionDef);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("[KiiroTradePatch] 注入绮罗交易收购器失败: " + e);
            }
        }

        /// <summary>判断是否为绮罗系派系（兼容第三方派系扩展）。</summary>
        private static bool IsKiiroFaction(FactionDef factionDef)
        {
            return factionDef.defName.StartsWith("Kiiro", StringComparison.OrdinalIgnoreCase)
                || (factionDef.categoryTag != null
                    && factionDef.categoryTag.StartsWith("Kiiro", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>对单个绮罗系派系应用：替换共享交易引用 + 清理并注入自有交易。</summary>
        private static void ApplyToFaction(FactionDef factionDef)
        {
            ReplaceSharedTrader(factionDef.baseTraderKinds, "Base_Outlander_Standard", "Kiiro_Base_Standard");
            ReplaceSharedTrader(factionDef.caravanTraderKinds, "Caravan_Outlander_BulkGoods", "Kiiro_Caravan_BulkGoods");
            ReplaceSharedTrader(factionDef.caravanTraderKinds, "Caravan_Outlander_Exotic", "Kiiro_Caravan_Exotic");
            // 动态克隆隔离：派系扩展若引用了被鼠族注入的共享商队（作战补给商/海盗商/轨道/帝国等），
            // 先替换为绮罗专属克隆体，再由下方清理/注入循环统一处理（操作顺序不能颠倒）。
            ReplaceContaminatedSharedTraders(factionDef);

            foreach (TraderKindDef trader in factionDef.baseTraderKinds
                .Concat(factionDef.caravanTraderKinds)
                .Concat(factionDef.visitorTraderKinds)
                .Concat(factionDef.orbitalTraderKinds))
            {
                if (trader == null)
                {
                    continue;
                }
                // 处理绮罗自有交易定义：清理鼠族收购 tag、拼写错误的收购 tag、
                // 按全局衣物/武器 tag 出售非绮罗物品的出售器，
                // 并注入绮罗衣物/武器收购器与出售器。
                // 共享定义（如 Visitor_Outlander_Standard 被多个派系共用）不在此处理，避免污染其他派系。
                if (trader.defName.StartsWith("Kiiro", StringComparison.OrdinalIgnoreCase))
                {
                    trader.stockGenerators.RemoveAll(sg =>
                        IsRatkinBuyTag(sg) || IsMisspelledKiiroBuyTag(sg)
                        || IsGlobalClothingSellGenerator(sg) || IsGlobalWeaponSellGenerator(sg));
                    if (!trader.stockGenerators.Any(sg => sg is StockGenerator_BuyKiiroApparel))
                    {
                        trader.stockGenerators.Add(new StockGenerator_BuyKiiroApparel());
                    }
                    if (!trader.stockGenerators.Any(sg => sg is StockGenerator_SellKiiroApparel))
                    {
                        trader.stockGenerators.Add(new StockGenerator_SellKiiroApparel());
                    }
                    if (!trader.stockGenerators.Any(sg => sg is StockGenerator_SellKiiroWeapon))
                    {
                        trader.stockGenerators.Add(new StockGenerator_SellKiiroWeapon());
                    }
                }
            }
        }

        /// <summary>把派系引用的原版共享交易替换为绮罗专属交易（若专属定义存在则替换）。</summary>
        private static void ReplaceSharedTrader(List<TraderKindDef> list, string fromDefName, string toDefName)
        {
            TraderKindDef replacement = DefDatabase<TraderKindDef>.GetNamedSilentFail(toDefName);
            if (replacement == null)
            {
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].defName == fromDefName)
                {
                    list[i] = replacement;
                }
            }
        }

        /// <summary>动态克隆隔离：遍历派系四个交易列表，把被鼠族污染的共享交易替换为绮罗专属克隆体。</summary>
        private static void ReplaceContaminatedSharedTraders(FactionDef factionDef)
        {
            ReplaceContaminatedIn(factionDef.baseTraderKinds);
            ReplaceContaminatedIn(factionDef.caravanTraderKinds);
            ReplaceContaminatedIn(factionDef.visitorTraderKinds);
            ReplaceContaminatedIn(factionDef.orbitalTraderKinds);
        }

        /// <summary>把列表中含鼠族收购 tag 的共享交易替换为克隆体（自有/专属定义跳过）。</summary>
        private static void ReplaceContaminatedIn(List<TraderKindDef> traders)
        {
            if (traders == null)
            {
                return;
            }
            for (int i = 0; i < traders.Count; i++)
            {
                TraderKindDef def = traders[i];
                if (def == null || def.defName.StartsWith("Kiiro", StringComparison.OrdinalIgnoreCase))
                {
                    continue;   // 绮罗自有/专属定义由清理循环处理，无需克隆
                }
                if (!HasRatkinBuyTag(def))
                {
                    continue;   // 未被污染，保持原共享定义
                }
                traders[i] = EnsureClone(def);
            }
        }

        /// <summary>判断交易定义是否含鼠族收购 tag（被鼠族注入污染）。</summary>
        private static bool HasRatkinBuyTag(TraderKindDef def)
        {
            if (def.stockGenerators == null)
            {
                return false;
            }
            for (int i = 0; i < def.stockGenerators.Count; i++)
            {
                if (IsRatkinBuyTag(def.stockGenerators[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>克隆缓存：按原定义 defName 缓存克隆体，保证同一定义只克隆一次。</summary>
        private static readonly Dictionary<string, TraderKindDef> cloneCache = new Dictionary<string, TraderKindDef>();

        /// <summary>
        /// 克隆被污染的共享交易定义为绮罗专属副本（defName = "Kiiro_Clone_" + 原名）并注册到 DefDatabase。
        /// stockGenerators 新建列表（浅拷贝元素），后续清理/注入只影响克隆体，不影响原共享定义。
        /// </summary>
        private static TraderKindDef EnsureClone(TraderKindDef original)
        {
            if (cloneCache.TryGetValue(original.defName, out TraderKindDef cached))
            {
                return cached;
            }
            TraderKindDef clone = new TraderKindDef
            {
                defName = "Kiiro_" + original.defName,
                label = original.label,
                orbital = original.orbital,
                requestable = original.requestable,
                hideThingsNotWillingToTrade = original.hideThingsNotWillingToTrade,
                commonality = original.commonality,
                category = original.category,
                tradeCurrency = original.tradeCurrency,
                commonalityMultFromPopulationIntent = original.commonalityMultFromPopulationIntent,
                faction = original.faction,
                permitRequiredForTrading = original.permitRequiredForTrading,
                stockGenerators = new List<StockGenerator>(original.stockGenerators)
            };
            DefDatabase<TraderKindDef>.Add(clone);
            cloneCache[original.defName] = clone;
            return clone;
        }

        /// <summary>鼠族 mod 通过全局共享交易定义添加的收购 tag。</summary>
        private static bool IsRatkinBuyTag(StockGenerator sg)
        {
            return sg is StockGenerator_BuyTradeTag buyTradeTag
                && (buyTradeTag.tag == "RK_Apparel"
                    || buyTradeTag.tag == "RK_TradeTag_All"
                    || buyTradeTag.tag == "RK_TradeTag_ArmorHighTech");
        }

        /// <summary>绮罗本体商队定义中拼写错误的收购 tag（缺少下划线，无法匹配任何衣物）。</summary>
        private static bool IsMisspelledKiiroBuyTag(StockGenerator sg)
        {
            return sg is StockGenerator_BuyTradeTag buyTradeTag && buyTradeTag.tag == "KiiroDailyClothing";
        }

        /// <summary>
        /// 按全局衣物 tag 出售非绮罗衣物的出售器（MarketValue / StockGenerator_Tag）。
        /// 这些 tag 只挂在人类/鼠族等其他种族衣物上，绮罗衣物仅有 Kiiro_ 自定义 tag，
        /// 因此移除后绮罗交易点不再出售非绮罗衣物。
        /// </summary>
        private static bool IsGlobalClothingSellGenerator(StockGenerator sg)
        {
            if (sg is StockGenerator_MarketValue marketValue)
            {
                return marketValue.tradeTag == "BasicClothing"
                    || marketValue.tradeTag == "Clothing"
                    || marketValue.tradeTag == "Armor";
            }
            if (sg is StockGenerator_Tag tagGenerator)
            {
                return tagGenerator.tradeTag == "BasicClothing"
                    || tagGenerator.tradeTag == "Clothing"
                    || tagGenerator.tradeTag == "Armor";
            }
            return false;
        }

        /// <summary>
        /// 按全局武器规则出售非绮罗武器的出售器：
        /// - StockGenerator_MarketValue / StockGenerator_Tag 按 WeaponRanged / WeaponMelee 全局 tag 出售；
        /// - StockGenerator_Category 按 WeaponsMelee 全局类别出售（categoryDef 为私有字段，用反射读取）。
        /// </summary>
        private static bool IsGlobalWeaponSellGenerator(StockGenerator sg)
        {
            if (sg is StockGenerator_MarketValue marketValue)
            {
                return marketValue.tradeTag == "WeaponRanged" || marketValue.tradeTag == "WeaponMelee";
            }
            if (sg is StockGenerator_Tag tagGenerator)
            {
                return tagGenerator.tradeTag == "WeaponRanged" || tagGenerator.tradeTag == "WeaponMelee";
            }
            if (sg is StockGenerator_Category category)
            {
                ThingCategoryDef categoryDef = categoryDefField.GetValue(category) as ThingCategoryDef;
                return categoryDef != null && categoryDef.defName == "WeaponsMelee";
            }
            return false;
        }

        /// <summary>StockGenerator_Category.categoryDef 私有字段的反射访问器（字段无公开读取途径）。</summary>
        private static readonly FieldInfo categoryDefField =
            typeof(StockGenerator_Category).GetField("categoryDef", BindingFlags.NonPublic | BindingFlags.Instance);
    }
}
