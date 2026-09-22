using NUnit.Framework;
using DungeonCrawler.Core;

namespace DungeonCrawler.Tests
{
    /// <summary>
    /// The automated playtest pass. These runs do not assert that the game is fun — only that a full
    /// game can be played from floor 1 to the end without soft-locking, throwing, or breaking a rule.
    /// </summary>
    public class SelfPlayTests
    {
        [Test]
        public void TwentyFullRunsCompleteWithoutBreakingAnything()
        {
            for (int i = 0; i < 20; i++)
            {
                int seed = 1000 + i * 977;
                SelfPlayReport report = SelfPlay.Run(seed);
                Assert.IsTrue(report.Passed, report.ToString());
                Assert.IsFalse(report.HitTurnLimit, "run never finished: " + report);
            }
        }

        [Test]
        public void TheFullDungeonIsClearableAndNotACakewalk()
        {
            // Fixed seeds and a deterministic RNG make this a regression check, not a coin flip.
            // A dungeon nobody can clear is as broken as one that cannot kill you.
            int victories = 0;
            int deaths = 0;
            int totalDepth = 0;
            const int runs = 40;

            for (int i = 0; i < runs; i++)
            {
                SelfPlayReport report = SelfPlay.Run(1 + i * 7919);
                Assert.IsTrue(report.Passed, report.ToString());

                if (report.Status == GameStatus.Victory) victories++;
                if (report.Status == GameStatus.Dead) deaths++;
                totalDepth += report.DeepestDepth;
            }

            double averageDepth = totalDepth / (double)runs;
            Assert.GreaterOrEqual(victories, 2, "the scripted agent cleared the dungeon " + victories + "/" + runs + " times");
            Assert.GreaterOrEqual(deaths, 5, "monsters killed the agent only " + deaths + "/" + runs + " times");
            Assert.GreaterOrEqual(averageDepth, 4.0, "average depth reached was only " + averageDepth);
        }

        [Test]
        public void ShallowFloorsAreSurvivableForANewCharacter()
        {
            // Floor 1 to 3 should not be a coin flip; the agent plays them near-perfectly.
            var config = new GameConfig { MaxDepth = 3 };
            int survived = 0;
            for (int i = 0; i < 20; i++)
            {
                SelfPlayReport report = SelfPlay.Run(7000 + i * 131, 6000, config);
                Assert.IsTrue(report.Passed, report.ToString());
                if (report.Status == GameStatus.Victory) survived++;
            }
            Assert.GreaterOrEqual(survived, 14, "only " + survived + "/20 cleared three floors");
        }

        [Test]
        public void EveryFloorValidatesDuringARealRun()
        {
            var game = new GameState(4321);
            game.StartNewRun();

            for (int depth = 1; depth <= game.MaxDepth; depth++)
            {
                Assert.IsTrue(game.LastValidation.IsValid, "floor " + game.Depth + ": " + game.LastValidation);
                Assert.IsNull(SelfPlay.CheckInvariants(game));

                Vec2I step = Pathfinding.FirstStepToward(game.Dungeon, game.Player.Position, game.Dungeon.StairsDown);
                Assert.AreNotEqual(Vec2I.Zero, step, "stairs unreachable on floor " + game.Depth);

                game.Player.Position = game.Dungeon.StairsDown;
                game.PlayerDescend();
            }

            Assert.AreEqual(GameStatus.Victory, game.Status);
        }

        [Test]
        public void RunsAreReproducibleFromTheirSeed()
        {
            SelfPlayReport first = SelfPlay.Run(31415);
            SelfPlayReport second = SelfPlay.Run(31415);

            Assert.AreEqual(first.Status, second.Status);
            Assert.AreEqual(first.Turns, second.Turns);
            Assert.AreEqual(first.Kills, second.Kills);
            Assert.AreEqual(first.DeepestDepth, second.DeepestDepth);
        }
    }
}
