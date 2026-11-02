using System;
using KNL;
using UnityEngine;
// Suppress Enemy's manager callbacks; this fixture exercises its actual type/death contract.
class IndicatorEnemy : Enemy { void Awake() {} new void OnEnable() {} new void OnDisable() {} }
class EnemyIndicatorRegression
{
    static int passed;
    static void Check(bool value, string name)
    { if (!value) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
    static void Main()
    {
        KNL.World.ActiveScene = new SceneTag("enemy indicator regression");
        var g = new GameObject("enemy");
        var enemy = g.AddComponent<IndicatorEnemy>();
        var damage = g.AddComponent<Damageable>();
        var dead = typeof(Damageable).GetProperty("isDead");
        Check(EdgeIndicators.IsLivingEnemy(enemy), "living enemy remains eligible");
        dead.SetValue(damage, true, null);
        Check(!EdgeIndicators.IsLivingEnemy(enemy), "dead enemy excluded before corpse destruction");
        dead.SetValue(damage, false, null);
        Check(EdgeIndicators.IsLivingEnemy(enemy), "reused enemy eligible after reset");
        enemy.enabled = false;
        Check(!EdgeIndicators.IsLivingEnemy(enemy), "disabled enemy excluded");
        enemy.enabled = true; g.SetActive(false);
        Check(!EdgeIndicators.IsLivingEnemy(enemy), "inactive enemy excluded");
        g.SetActive(true);
        g.AddComponent<SpriteRenderer>().color = new Color(1, 1, 1, 0);
        Check(EdgeIndicators.IsLivingEnemy(enemy), "transparent live spawn remains eligible");
        Check(!EdgeIndicators.IsLivingEnemy(new GameObject("other").AddComponent<SpriteRenderer>()), "nonenemy excluded");
        UnityEngine.Object.DestroyImmediate(g);
        Check(!EdgeIndicators.IsLivingEnemy(enemy), "destroyed enemy excluded");
        Check(Lifecycle.Exceptions == 0, "fixture has no lifecycle exceptions");
        Console.WriteLine("Enemy indicator checks passed: " + passed);
    }
}
