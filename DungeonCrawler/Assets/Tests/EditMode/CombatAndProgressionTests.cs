using NUnit.Framework;
using DungeonCrawler.Core;

namespace DungeonCrawler.Tests
{
    public class CombatAndProgressionTests
    {
        [Test]
        public void DamageNeverDropsBelowOne()
        {
            var rng = new Rng(5);
            for (int i = 0; i < 500; i++)
            {
                Assert.GreaterOrEqual(Combat.RollDamage(1, 99, rng), Combat.MinDamage);
            }
        }

        [Test]
        public void DamageStaysWithinTheFormulaEnvelope()
        {
            var rng = new Rng(11);
            for (int i = 0; i < 500; i++)
            {
                int damage = Combat.RollDamage(10, 3, rng);
                Assert.GreaterOrEqual(damage, 5);
                Assert.LessOrEqual(damage, 9);
            }
        }

        [Test]
        public void AttackKillsAndReportsIt()
        {
            var attacker = new Enemy(EnemyCatalog.OrcBrute, 1, Vec2I.Zero);
            var victim = new Enemy(EnemyCatalog.GiantRat, 1, new Vec2I(1, 0));
            victim.Hp = 1;

            AttackResult result = Combat.Resolve(attacker, victim, new Rng(3));

            Assert.IsTrue(result.KilledDefender);
            Assert.IsFalse(victim.IsAlive);
            Assert.AreEqual(1, result.Damage, "damage is clipped to the HP actually removed");
            StringAssert.Contains("The orc brute hits the giant rat", result.Describe());
            StringAssert.Contains("The giant rat dies!", result.Describe());
        }

        [Test]
        public void CombatLinesReadCorrectlyForThePlayer()
        {
            var player = new Player();
            var goblin = new Enemy(EnemyCatalog.Goblin, 1, new Vec2I(1, 0));
            goblin.Hp = 1;

            string killing = Combat.Resolve(player, goblin, new Rng(7)).Describe();
            StringAssert.StartsWith("You hit the goblin for", killing);
            StringAssert.Contains("The goblin dies!", killing);

            var archer = new Enemy(EnemyCatalog.SkeletonArcher, 1, new Vec2I(4, 0));
            player.Hp = 1;
            string death = Combat.Resolve(archer, player, new Rng(7), true).Describe();
            StringAssert.StartsWith("The skeleton archer shoots you for", death);
            StringAssert.Contains("You die!", death);
        }

        [Test]
        public void ArticlesMatchTheMonsterName()
        {
            Assert.AreEqual("a goblin", Phrasing.WithArticle("goblin"));
            Assert.AreEqual("an orc brute", Phrasing.WithArticle("orc brute"));
        }

        [Test]
        public void HealNeverExceedsMaxHp()
        {
            var player = new Player();
            player.TakeDamage(10);
            player.Heal(1000);
            Assert.AreEqual(player.MaxHp, player.Hp);
        }

        [Test]
        public void LevelUpRaisesStatsAndCarriesLeftoverXp()
        {
            var player = new Player();
            int need = player.XpToNextLevel;
            int levels = player.GainXp(need + 3);

            Assert.AreEqual(1, levels);
            Assert.AreEqual(2, player.Level);
            Assert.AreEqual(3, player.Xp);
            Assert.AreEqual(Player.StartingMaxHp + 7, player.MaxHp);
            Assert.AreEqual(Player.StartingAttack + 1, player.Attack);
        }

        [Test]
        public void ASingleHugeXpAwardCanGrantSeveralLevels()
        {
            var player = new Player();
            int levels = player.GainXp(1000);
            Assert.Greater(levels, 3);
            Assert.AreEqual(1 + levels, player.Level);
        }

        [Test]
        public void EquipmentAndBuffsFeedTotalStats()
        {
            var player = new Player();
            int baseAttack = player.TotalAttack;

            player.Equip(new Item(ItemCatalog.BattleAxe));
            Assert.AreEqual(baseAttack + ItemCatalog.BattleAxe.Power, player.TotalAttack);

            player.ApplyBuff(BuffKind.Attack, 4, 2);
            Assert.AreEqual(baseAttack + ItemCatalog.BattleAxe.Power + 4, player.TotalAttack);

            player.TickBuffs();
            Assert.AreEqual(baseAttack + ItemCatalog.BattleAxe.Power + 4, player.TotalAttack, "buff still has a turn left");
            player.TickBuffs();
            Assert.AreEqual(baseAttack + ItemCatalog.BattleAxe.Power, player.TotalAttack, "buff expired");
        }

        [Test]
        public void EquippingReturnsTheReplacedPiece()
        {
            var player = new Player();
            var dagger = new Item(ItemCatalog.Dagger);
            var axe = new Item(ItemCatalog.BattleAxe);

            Assert.IsNull(player.Equip(dagger));
            Assert.AreSame(dagger, player.Equip(axe));
            Assert.AreSame(axe, player.Weapon);
        }

        [Test]
        public void EnemyStatsScaleWithDepth()
        {
            var shallow = new Enemy(EnemyCatalog.Goblin, 1, Vec2I.Zero);
            var deep = new Enemy(EnemyCatalog.Goblin, 8, Vec2I.Zero);

            Assert.Greater(deep.MaxHp, shallow.MaxHp);
            Assert.Greater(deep.Attack, shallow.Attack);
            Assert.Greater(deep.XpValue, shallow.XpValue);
            Assert.AreEqual(deep.MaxHp, deep.Hp, "enemies spawn at full health");
        }

        [Test]
        public void LootTableRespectsDepthGates()
        {
            var rng = new Rng(31337);
            for (int i = 0; i < 400; i++)
            {
                Item item = LootTable.Roll(1, rng);
                Assert.AreEqual(1, item.Def.MinDepth, item.Def.Name + " should not drop on floor 1");
            }
        }

        [Test]
        public void GoldRollsScaleWithDepth()
        {
            var rng = new Rng(808);
            int shallow = LootTable.Create(ItemCatalog.GoldPile, 1, rng).Amount;
            int deep = LootTable.Create(ItemCatalog.GoldPile, 8, rng).Amount;
            Assert.Greater(deep, shallow);
        }

        [Test]
        public void EnemyRollNeverReturnsAnUnlockedArchetype()
        {
            var rng = new Rng(4);
            for (int depth = 1; depth <= 8; depth++)
            {
                for (int i = 0; i < 200; i++)
                {
                    EnemyArchetype archetype = EnemyCatalog.Roll(depth, rng);
                    Assert.LessOrEqual(archetype.MinDepth, depth,
                        archetype.Name + " appeared on floor " + depth);
                }
            }
        }

        [Test]
        public void InventoryIsCapped()
        {
            var pack = new Inventory(3);
            Assert.IsTrue(pack.TryAdd(new Item(ItemCatalog.Dagger)));
            Assert.IsTrue(pack.TryAdd(new Item(ItemCatalog.Dagger)));
            Assert.IsTrue(pack.TryAdd(new Item(ItemCatalog.Dagger)));
            Assert.IsFalse(pack.TryAdd(new Item(ItemCatalog.Dagger)));
            Assert.AreEqual(3, pack.Count);
        }
    }
}
