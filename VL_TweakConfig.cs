using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace ValheimLegends
{
    /// <summary>
    /// Sistema de config propio (fork) para sobreescribir cooldowns por-habilidad
    /// y tweaks de Druid/Priest, todo editable desde el .cfg sin recompilar.
    ///
    /// Regla: cada entrada vale -1 por DEFAULT => se usa el valor original de VL
    /// (comportamiento idéntico al vanilla). Poné un valor >= 0 para overridear.
    ///
    /// Cooldowns se expresan en SEGUNDOS y NO se multiplican por g_CooldownModifer
    /// cuando hay override (vos pediste manejar el CD exacto desde el cfg).
    /// </summary>
    public static class VL_TweakConfig
    {
        private const string SEC_CD   = "Tweaks Cooldowns";
        private const string SEC_DRU  = "Tweaks Druid";
        private const string SEC_PRI  = "Tweaks Priest";
        private const string SEC_VAL  = "Tweaks Valkyrie Taunt";
        private const string SEC_PAN  = "Class Pantheon";

        private static readonly Dictionary<string, ConfigEntry<float>> _cd =
            new Dictionary<string, ConfigEntry<float>>();

        // ---- Druid Regeneration ----
        public static ConfigEntry<float> Druid_RegenDuration;     // TTL total del HoT (seg)
        public static ConfigEntry<float> Druid_RegenInterval;     // cada cuántos seg cura
        public static ConfigEntry<bool>  Druid_RegenCleansePoison; // quita veneno al activar

        // ---- Priest Heal (ability3) ----
        public static ConfigEntry<bool>  Priest_HealOverTime;     // true = HoT en vez de instantáneo
        public static ConfigEntry<float> Priest_HealDuration;     // duración del HoT (seg)
        public static ConfigEntry<float> Priest_HealInterval;     // tick del HoT (seg)
        public static ConfigEntry<float> Priest_HealIntFactor;    // % extra de heal por punto de Intellect (EpicMMO)

        // Purge (Ability2) reconfigurado a hielo + congelamiento
        public static ConfigEntry<float> Priest_PurgeFrostDamage; // daño de hielo base
        public static ConfigEntry<float> Priest_PurgeFrostScale;  // + daño por nivel de skill Purge
        public static ConfigEntry<float> Priest_PurgeRadius;      // área del Purge
        public static ConfigEntry<bool>  Priest_PurgeFreeze;      // aplica congelamiento
        public static ConfigEntry<float> Priest_PurgeFreezeDur;   // duración del congelamiento (seg)

        // ---- Valkyrie Taunt (todas las habilidades) ----
        public static ConfigEntry<bool>  Valk_TauntEnabled;       // master switch
        public static ConfigEntry<float> Valk_TauntRadius;        // radio en metros
        public static ConfigEntry<float> Valk_TauntDuration;      // duración en seg
        public static ConfigEntry<bool>  Valk_TauntBulwark;       // ability1
        public static ConfigEntry<bool>  Valk_TauntStagger;       // ability2
        public static ConfigEntry<bool>  Valk_TauntLeap;          // ability3

        // ================= PANTHEON =================
        // Pasiva Mortal Will
        public static ConfigEntry<int>   Pan_MortalWillStacks;    // cargas necesarias (def 5)
        public static ConfigEntry<float> Pan_MortalWillMult;      // multiplicador de daño al estar empoderado

        // Q - Comet Spear (lanza lanzas tipo cuchillo)
        public static ConfigEntry<float> Pan_Q_Damage;            // daño pierce base
        public static ConfigEntry<float> Pan_Q_DamageScale;       // + por nivel de skill
        public static ConfigEntry<float> Pan_Q_Cost;              // stamina
        public static ConfigEntry<float> Pan_Q_Cooldown;          // seg
        public static ConfigEntry<float> Pan_Q_Speed;             // velocidad del proyectil

        // E - Aegis Assault (activable con duración: bloquea 100% al frente + fuego)
        public static ConfigEntry<float> Pan_E_Duration;          // cuánto dura el escudo (seg)
        public static ConfigEntry<float> Pan_E_FireDmgPerSec;     // daño fuego base / seg al frente
        public static ConfigEntry<float> Pan_E_FireDmgPerStrength;// daño fuego extra por punto de Strength (EpicMMO)
        public static ConfigEntry<float> Pan_E_Range;             // alcance del cono frontal
        public static ConfigEntry<float> Pan_E_Angle;             // ángulo del cono de bloqueo/daño (def 30)
        public static ConfigEntry<float> Pan_E_Cost;              // stamina al activar
        public static ConfigEntry<float> Pan_E_Cooldown;          // cooldown (seg)

        // R - Aether Leap (salto telegrafiado + caída con lanza, escala con Strength)
        public static ConfigEntry<float> Pan_R_JumpHeight;        // altura del salto
        public static ConfigEntry<float> Pan_R_HoverTime;         // máx seg flotando apuntando
        public static ConfigEntry<float> Pan_R_AoeRadius;         // radio del impacto
        public static ConfigEntry<float> Pan_R_ImpactDamage;      // daño base al caer
        public static ConfigEntry<float> Pan_R_DamagePerStrength; // + daño por punto de Strength
        public static ConfigEntry<float> Pan_R_StunDuration;      // duración base del stun
        public static ConfigEntry<float> Pan_R_StunPerStrength;   // + stun por punto de Strength
        public static ConfigEntry<float> Pan_R_Cost;              // stamina
        public static ConfigEntry<float> Pan_R_Cooldown;          // seg
        public static ConfigEntry<string> Pan_Item;               // item de transformación

        private static bool _inited;

        // Pares (clave de cfg, cooldown default original de VL en segundos).
        private static readonly (string key, float def)[] _cdDefs = new (string, float)[]
        {
            ("ZoneCharge",   180f),
            ("Weaken",        30f),
            ("Charm",         60f),
            ("MeteorPunch",    1f),
            ("PsiBolt",        1f),
            ("FlyingKick",     6f),
            ("PoisonBomb",    30f),
            ("Backstab",      20f),
            ("Fade",          15f),
            ("Sanctify",      45f),
            ("Heal",          30f),
            ("Purge",         15f),
            ("QuickShot",     10f),
            ("Riposte",        6f),
            ("BlinkStrike",   30f),
            ("Light",         20f),
            ("Warp",           6f),
            ("Replica",       30f),
            ("ForceWave",     20f),
            ("Fireball",      12f),
            ("Meteor",       180f),
            ("FrostNova",     20f),
            ("Bulwark",       60f),
            ("Leap",          15f),
            ("Stagger",       20f),
            ("HarpoonPull",   10f),
            ("Regeneration",  60f),
            ("Root",          20f),
            ("Defender",     120f),
            ("Enrage",        60f),
            ("SpiritBomb",    30f),
            ("Shell",        120f),
            ("Dash",          10f),  // ajustá si tu fork difiere
            ("Berserk",       30f),
            ("Execute",       20f),
            ("PowerShot",     20f),
            ("ShadowStalk",   30f),
            ("SummonWolf",    30f),
        };

        public static void Init(ConfigFile cfg)
        {
            if (_inited) return;
            _inited = true;

            try
            {
                foreach (var d in _cdDefs)
                {
                    _cd[d.key] = cfg.Bind(
                        SEC_CD,
                        d.key + "_Cooldown",
                        -1f,
                        $"Cooldown de {d.key} en segundos. -1 = usar default de VL ({d.def}s).");
                }

                Druid_RegenDuration = cfg.Bind(SEC_DRU, "Regen_Duration", 60f,
                    "Duración total del heal-over-time de Regeneration (segundos).");
                Druid_RegenInterval = cfg.Bind(SEC_DRU, "Regen_TickInterval", 2f,
                    "Cada cuántos segundos cura Regeneration.");
                Druid_RegenCleansePoison = cfg.Bind(SEC_DRU, "Regen_CleansePoison", true,
                    "Si true, al activar Regeneration quita el veneno a los aliados curados.");

                Priest_HealOverTime = cfg.Bind(SEC_PRI, "Heal_OverTime", true,
                    "Si true, el heal del Priest (ability3) cura a lo largo del tiempo en vez de instantáneo.");
                Priest_HealDuration = cfg.Bind(SEC_PRI, "Heal_Duration", 10f,
                    "Duración del heal-over-time del Priest (segundos).");
                Priest_HealInterval = cfg.Bind(SEC_PRI, "Heal_TickInterval", 1f,
                    "Cada cuántos segundos cura el HoT del Priest.");
                Priest_HealIntFactor = cfg.Bind(SEC_PRI, "Heal_IntellectFactor", 2f,
                    "% extra de curación por cada punto de Intellect de EpicMMO. Ej: 2 = +2% por punto. 0 = sin escalado.");

                Priest_PurgeFrostDamage = cfg.Bind(SEC_PRI, "Purge_FrostDamage", 20f,
                    "Daño de hielo base del Purge.");
                Priest_PurgeFrostScale = cfg.Bind(SEC_PRI, "Purge_FrostDamageScale", 1f,
                    "Daño de hielo extra por nivel de skill de Purge.");
                Priest_PurgeRadius = cfg.Bind(SEC_PRI, "Purge_Radius", 20f,
                    "Radio del área de efecto del Purge.");
                Priest_PurgeFreeze = cfg.Bind(SEC_PRI, "Purge_ApplyFreeze", true,
                    "Si true, el Purge congela a los enemigos golpeados.");
                Priest_PurgeFreezeDur = cfg.Bind(SEC_PRI, "Purge_FreezeDuration", 4f,
                    "Duración del congelamiento (seg).");

                Valk_TauntEnabled = cfg.Bind(SEC_VAL, "Taunt_Enabled", true,
                    "Activa el taunt al usar habilidades de la Valkyrie.");
                Valk_TauntRadius = cfg.Bind(SEC_VAL, "Taunt_Radius", 20f,
                    "Radio del taunt en metros.");
                Valk_TauntDuration = cfg.Bind(SEC_VAL, "Taunt_Duration", 5f,
                    "Duración del taunt en segundos (los enemigos te siguen forzados).");
                Valk_TauntBulwark = cfg.Bind(SEC_VAL, "Taunt_OnBulwark", true,
                    "Tauntea al usar Bulwark (ability 1).");
                Valk_TauntStagger = cfg.Bind(SEC_VAL, "Taunt_OnStagger", true,
                    "Tauntea al usar Stagger (ability 2).");
                Valk_TauntLeap = cfg.Bind(SEC_VAL, "Taunt_OnLeap", true,
                    "Tauntea al usar Leap (ability 3).");

                Pan_MortalWillStacks = cfg.Bind(SEC_PAN, "MortalWill_Stacks", 5,
                    "Cargas necesarias para empoderar la siguiente habilidad.");
                Pan_MortalWillMult = cfg.Bind(SEC_PAN, "MortalWill_DamageMult", 2f,
                    "Multiplicador de daño cuando la habilidad sale empoderada.");

                Pan_Q_Damage = cfg.Bind(SEC_PAN, "Q_Damage", 30f,
                    "Daño base (pierce) de la lanza.");
                Pan_Q_DamageScale = cfg.Bind(SEC_PAN, "Q_DamageScale", 1.5f,
                    "Daño extra por nivel de skill.");
                Pan_Q_Cost = cfg.Bind(SEC_PAN, "Q_StaminaCost", 15f, "Stamina de Comet Spear.");
                Pan_Q_Cooldown = cfg.Bind(SEC_PAN, "Q_Cooldown", 3f, "Cooldown de Comet Spear (seg).");
                Pan_Q_Speed = cfg.Bind(SEC_PAN, "Q_ProjectileSpeed", 35f, "Velocidad de la lanza.");

                Pan_E_Duration = cfg.Bind(SEC_PAN, "E_Duration", 6f,
                    "Cuántos segundos dura Aegis (bloqueo frontal 100% + daño de fuego).");
                Pan_E_FireDmgPerSec = cfg.Bind(SEC_PAN, "E_FireDamagePerSec", 25f,
                    "Daño de fuego base por segundo a enemigos frente al escudo.");
                Pan_E_FireDmgPerStrength = cfg.Bind(SEC_PAN, "E_FireDamagePerStrength", 1f,
                    "Daño de fuego extra por cada punto de Strength de EpicMMO (0 = sin escalado).");
                Pan_E_Range = cfg.Bind(SEC_PAN, "E_Range", 6f, "Alcance del cono frontal.");
                Pan_E_Angle = cfg.Bind(SEC_PAN, "E_Angle", 30f,
                    "Ángulo del cono frontal en grados: bloquea 100% del daño y aplica fuego dentro de este cono.");
                Pan_E_Cost = cfg.Bind(SEC_PAN, "E_StaminaCost", 10f, "Stamina al activar Aegis.");
                Pan_E_Cooldown = cfg.Bind(SEC_PAN, "E_Cooldown", 12f, "Cooldown de Aegis (seg).");

                Pan_R_JumpHeight = cfg.Bind(SEC_PAN, "R_JumpHeight", 15f,
                    "Fuerza del impulso vertical del salto (la Valkyrie usa 15; más = más alto).");
                Pan_R_HoverTime = cfg.Bind(SEC_PAN, "R_HoverTime", 2f,
                    "Segundos máximos flotando para apuntar el destino.");
                Pan_R_AoeRadius = cfg.Bind(SEC_PAN, "R_AoeRadius", 8f,
                    "Radio del área de impacto al caer.");
                Pan_R_ImpactDamage = cfg.Bind(SEC_PAN, "R_ImpactDamage", 60f,
                    "Daño base (pierce) al caer.");
                Pan_R_DamagePerStrength = cfg.Bind(SEC_PAN, "R_DamagePerStrength", 2f,
                    "Daño extra por punto de Strength de EpicMMO.");
                Pan_R_StunDuration = cfg.Bind(SEC_PAN, "R_StunDuration", 3f,
                    "Duración base del stun a los enemigos golpeados (seg).");
                Pan_R_StunPerStrength = cfg.Bind(SEC_PAN, "R_StunPerStrength", 0.05f,
                    "Stun extra por punto de Strength (seg).");
                Pan_R_Cost = cfg.Bind(SEC_PAN, "R_StaminaCost", 30f, "Stamina del salto.");
                Pan_R_Cooldown = cfg.Bind(SEC_PAN, "R_Cooldown", 18f, "Cooldown del salto (seg).");
                Pan_Item = cfg.Bind(SEC_PAN, "Pantheon_Item", "item_surtlingcore",
                    "Sacrificá este item en el altar de Eikthyr para volverte Pantheon. Vacío = deshabilitar clase.");

                Debug.Log("[VL_TweakConfig] Config de tweaks inicializada.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VL_TweakConfig] Error al inicializar: {ex.Message}");
            }
        }

        /// <summary>
        /// Devuelve el cooldown configurado para 'key'. Si el cfg vale -1
        /// (o no se inicializó), devuelve el cálculo vanilla pasado en 'vanilla'.
        /// </summary>
        public static float CD(string key, float vanilla)
        {
            if (_cd.TryGetValue(key, out var entry) && entry.Value >= 0f)
                return entry.Value;
            return vanilla;
        }

        // Helpers tipados para SE_Regeneration (se usan también desde el Priest).
        public static float RegenDuration => Druid_RegenDuration != null ? Druid_RegenDuration.Value : 60f;
        public static float RegenInterval => Druid_RegenInterval != null ? Druid_RegenInterval.Value : 2f;

        // Multiplicador de heal del Priest según Intellect de EpicMMO.
        // heal_final = heal * (1 + Int * IntFactor/100)
        public static float PriestHealIntMult()
        {
            if (Priest_HealIntFactor == null) return 1f;
            int intel = VL_EpicMMOBridge.GetIntellect();
            return 1f + (intel * Priest_HealIntFactor.Value / 100f);
        }
    }
}