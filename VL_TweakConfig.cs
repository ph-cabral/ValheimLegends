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

        // Entradas sincronizadas servidor->cliente (usada por VL_ConfigSync). Key = clave del cfg.
        public static Dictionary<string, ConfigEntryBase> SyncedEntries = new Dictionary<string, ConfigEntryBase>();

        private static readonly Dictionary<string, ConfigEntry<float>> _cd =
            new Dictionary<string, ConfigEntry<float>>();

        // ---- Druid Shapeshift (block + Q/E/R) ----
        public static ConfigEntry<float> Druid_DragonDrain;
        public static ConfigEntry<float> Druid_DragonResist;
        public static ConfigEntry<float> Druid_DragonDamage;
        public static ConfigEntry<float> Druid_DragonScale;
        public static ConfigEntry<float> Druid_AboDrain;
        public static ConfigEntry<float> Druid_AboResist;
        public static ConfigEntry<float> Druid_AboDamage;
        public static ConfigEntry<float> Druid_AboScale;
        public static ConfigEntry<float> Druid_SerpentDrain;
        public static ConfigEntry<float> Druid_SerpentResist;
        public static ConfigEntry<float> Druid_SerpentSpeed;
        public static ConfigEntry<float> Druid_SerpentScale;

        // ---- Druid Regeneration ----
        public static ConfigEntry<float> Druid_RegenDuration;     // TTL total del HoT (seg)
        public static ConfigEntry<float> Druid_RegenInterval;     // cada cuántos seg cura
        public static ConfigEntry<bool>  Druid_RegenCleansePoison; // quita veneno al activar

        // ---- Druid Trophy Summon ----
        public static ConfigEntry<float> Druid_TrophyLeashDistance;   // distancia max antes de teleport
        public static ConfigEntry<float> Druid_TrophyFollowCheckRate; // cada cuantos seg revisar

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
        public static ConfigEntry<float> Priest_PurgeFreezeSlow;  // multiplicador de velocidad mientras congelado (0.4 = 40%)
        public static ConfigEntry<float> Priest_PurgeBaseCooldown;// cooldown base del Purge (seg)
        public static ConfigEntry<float> Priest_PurgeCdIntFactor; // seg de cooldown reducido por punto de Intellect
        public static ConfigEntry<float> Priest_PurgeCdMin;       // cooldown mínimo posible (seg)
        public static ConfigEntry<float> Priest_PurgeRadiusIntFactor; // metros extra de radio por punto de Intellect

        // ---- Priest Sanctify (ability1) ahora es heal-over-time ----
        public static ConfigEntry<float> Priest_SanctifyHealTotal;   // curación total repartida
        public static ConfigEntry<float> Priest_SanctifyDuration;    // duración del HoT (seg)
        public static ConfigEntry<float> Priest_SanctifyInterval;    // tick del HoT (seg)
        public static ConfigEntry<float> Priest_SanctifyRadius;      // radio del HoT
        public static ConfigEntry<float> Priest_SanctifyIntFactor;   // % extra de curación por punto de Intellect

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
        public static ConfigEntry<float> Pan_Q_ChargeForce;       // fuerza horizontal de la embestida (Q)
        public static ConfigEntry<float> Pan_Q_ChargeUp;          // componente vertical de la embestida
        public static ConfigEntry<float> Pan_Q_ChargeRadius;      // alcance del golpe de la embestida
        public static ConfigEntry<float> Pan_Q_StabRange;         // rango de la estocada larga (Q espada) - lo determina el usuario
        public static ConfigEntry<float> Pan_Q_StabAngle;         // ángulo del cono de la estocada (grados)
        public static ConfigEntry<float> Pan_Block_SpearDamage;   // daño pierce de la lanza (bloqueo+Q)
        public static ConfigEntry<float> Pan_Block_SpearStamina;  // stamina lanza (bloqueo+Q)

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

                Druid_DragonDrain  = cfg.Bind(SEC_DRU, "Dragon_StaminaDrainPerSec", 6f,
                    "Stamina por segundo que drena la forma Dragón (block + Q).");
                Druid_DragonResist = cfg.Bind(SEC_DRU, "Dragon_DamageTakenMult", 0.45f,
                    "Daño recibido en forma Dragón (0.45 = recibe 45%). Inmune al frío siempre.");
                Druid_DragonDamage = cfg.Bind(SEC_DRU, "Dragon_DamageMult", 1.8f,
                    "Multiplicador del daño infligido en forma Dragón.");
                Druid_DragonScale  = cfg.Bind(SEC_DRU, "Dragon_Scale", 2.2f,
                    "Escala del modelo en forma Dragón.");

                Druid_AboDrain  = cfg.Bind(SEC_DRU, "Abomination_StaminaDrainPerSec", 5f,
                    "Stamina por segundo que drena la forma Abominación (block + E).");
                Druid_AboResist = cfg.Bind(SEC_DRU, "Abomination_DamageTakenMult", 0.4f,
                    "Daño recibido en forma Abominación (0.4 = recibe 40%). Inmune al veneno siempre.");
                Druid_AboDamage = cfg.Bind(SEC_DRU, "Abomination_DamageMult", 2f,
                    "Multiplicador del daño infligido en forma Abominación.");
                Druid_AboScale  = cfg.Bind(SEC_DRU, "Abomination_Scale", 2f,
                    "Escala del modelo en forma Abominación.");

                Druid_SerpentDrain  = cfg.Bind(SEC_DRU, "Serpent_StaminaDrainPerSec", 4f,
                    "Stamina por segundo que drena la forma Serpiente de océano (block + R).");
                Druid_SerpentResist = cfg.Bind(SEC_DRU, "Serpent_DamageTakenMult", 0.5f,
                    "Daño recibido en forma Serpiente (0.5 = recibe 50%). No recibe daño de ahogamiento.");
                Druid_SerpentSpeed  = cfg.Bind(SEC_DRU, "Serpent_SpeedMult", 1.6f,
                    "Multiplicador de velocidad de movimiento en forma Serpiente.");
                Druid_SerpentScale  = cfg.Bind(SEC_DRU, "Serpent_Scale", 1.8f,
                    "Escala del modelo en forma Serpiente.");

                Druid_RegenDuration = cfg.Bind(SEC_DRU, "Regen_Duration", 60f,
                    "Duración total del heal-over-time de Regeneration (segundos).");
                Druid_RegenInterval = cfg.Bind(SEC_DRU, "Regen_TickInterval", 2f,
                    "Cada cuántos segundos cura Regeneration.");
                Druid_RegenCleansePoison = cfg.Bind(SEC_DRU, "Regen_CleansePoison", true,
                    "Si true, al activar Regeneration quita el veneno a los aliados curados.");

                Druid_TrophyLeashDistance = cfg.Bind(SEC_DRU, "TrophySummon_LeashDistance", 25f,
                    "Distancia maxima (en metros) a la que pueden estar los monstruos invocados por trofeo antes de ser forzados a teleportarse cerca del druida.");
                Druid_TrophyFollowCheckRate = cfg.Bind(SEC_DRU, "TrophySummon_FollowCheckRate", 2f,
                    "Cada cuantos segundos se revisa la distancia de los monstruos invocados por trofeo (menor = mas reactivo, mas costoso).");

                SyncedEntries["TrophySummon_LeashDistance"] = Druid_TrophyLeashDistance;
                SyncedEntries["TrophySummon_FollowCheckRate"] = Druid_TrophyFollowCheckRate;
                // (claves nuevas con prefijo vl_svr_ se registran más abajo)

                // Heal (ability3) = curación INSTANTÁNEA de un golpe (sin canalizar, sin HoT)
                Priest_HealOverTime = cfg.Bind(SEC_PRI, "Heal_OverTime", false,
                    "OBSOLETO. El Heal (ability3) ahora siempre es instantáneo de un solo golpe.");
                Priest_HealDuration = cfg.Bind(SEC_PRI, "Heal_Duration", 10f,
                    "OBSOLETO (Heal ya no usa HoT).");
                Priest_HealInterval = cfg.Bind(SEC_PRI, "Heal_TickInterval", 1f,
                    "OBSOLETO (Heal ya no usa HoT).");
                Priest_HealIntFactor = cfg.Bind(SEC_PRI, "Heal_IntellectFactor", 2f,
                    "% extra de curación instantánea por cada punto de Intellect de EpicMMO. Ej: 2 = +2% por punto. 0 = sin escalado.");

                // Sanctify (ability1) = heal-over-time
                Priest_SanctifyHealTotal = cfg.Bind(SEC_PRI, "Sanctify_HealTotal", 60f,
                    "Curación total que reparte Sanctify a lo largo de su duración.");
                Priest_SanctifyDuration = cfg.Bind(SEC_PRI, "Sanctify_Duration", 12f,
                    "Duración del heal-over-time de Sanctify (segundos).");
                Priest_SanctifyInterval = cfg.Bind(SEC_PRI, "Sanctify_TickInterval", 1f,
                    "Cada cuántos segundos cura el HoT de Sanctify.");
                Priest_SanctifyRadius = cfg.Bind(SEC_PRI, "Sanctify_Radius", 20f,
                    "Radio del heal-over-time de Sanctify.");
                Priest_SanctifyIntFactor = cfg.Bind(SEC_PRI, "Sanctify_IntellectFactor", 2f,
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
                Priest_PurgeFreezeSlow = cfg.Bind(SEC_PRI, "Purge_FreezeSlowMult", 0.3f,
                    "Velocidad del enemigo mientras está congelado (0.3 = 30% de su velocidad; más bajo = más lento).");
                Priest_PurgeBaseCooldown = cfg.Bind(SEC_PRI, "Purge_BaseCooldown", 15f,
                    "Cooldown base del Purge en segundos (antes de reducir por Intellect).");
                Priest_PurgeCdIntFactor = cfg.Bind(SEC_PRI, "Purge_CooldownIntellectFactor", 0.2f,
                    "Segundos de cooldown reducidos por cada punto de Intellect de EpicMMO. 0 = sin escalado.");
                Priest_PurgeCdMin = cfg.Bind(SEC_PRI, "Purge_CooldownMin", 4f,
                    "Cooldown mínimo del Purge tras aplicar el escalado por Intellect (seg).");
                Priest_PurgeRadiusIntFactor = cfg.Bind(SEC_PRI, "Purge_RadiusIntellectFactor", 0.15f,
                    "Metros extra de radio del Purge por cada punto de Intellect de EpicMMO. 0 = sin escalado.");

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
                Pan_Q_ChargeForce = cfg.Bind(SEC_PAN, "Q_ChargeForce", 22f,
                    "Fuerza horizontal de la embestida con lanza (Q).");
                Pan_Q_ChargeUp = cfg.Bind(SEC_PAN, "Q_ChargeUp", 4f,
                    "Componente vertical de la embestida.");
                Pan_Q_ChargeRadius = cfg.Bind(SEC_PAN, "Q_ChargeRadius", 3.5f,
                    "Alcance del golpe frontal de la embestida.");
                Pan_Q_StabRange = cfg.Bind(SEC_PAN, "Q_StabRange", 4f,
                    "Rango de la estocada larga de la Q (espada). Lo determina el usuario.");
                Pan_Q_StabAngle = cfg.Bind(SEC_PAN, "Q_StabAngle", 45f,
                    "Ángulo del cono frontal de la estocada larga (grados).");
                Pan_Block_SpearDamage = cfg.Bind(SEC_PAN, "Block_SpearDamage", 45f,
                    "Daño pierce de la lanza arrojadiza (bloqueo + Q).");
                Pan_Block_SpearStamina = cfg.Bind(SEC_PAN, "Block_SpearStamina", 12f,
                    "Stamina al lanzar la lanza (bloqueo + Q).");

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

                // ---- Registrar TODAS las entries como sincronizadas (server-authoritative) ----
                // Cooldowns por habilidad
                foreach (var kv in _cd)
                    SyncedEntries["vl_svr_" + kv.Key + "_Cooldown"] = kv.Value;

                // Druid
                SyncedEntries["vl_svr_Druid_DragonDrain"]            = Druid_DragonDrain;
                SyncedEntries["vl_svr_Druid_DragonResist"]           = Druid_DragonResist;
                SyncedEntries["vl_svr_Druid_DragonDamage"]           = Druid_DragonDamage;
                SyncedEntries["vl_svr_Druid_DragonScale"]            = Druid_DragonScale;
                SyncedEntries["vl_svr_Druid_AboDrain"]               = Druid_AboDrain;
                SyncedEntries["vl_svr_Druid_AboResist"]              = Druid_AboResist;
                SyncedEntries["vl_svr_Druid_AboDamage"]              = Druid_AboDamage;
                SyncedEntries["vl_svr_Druid_AboScale"]               = Druid_AboScale;
                SyncedEntries["vl_svr_Druid_SerpentDrain"]           = Druid_SerpentDrain;
                SyncedEntries["vl_svr_Druid_SerpentResist"]          = Druid_SerpentResist;
                SyncedEntries["vl_svr_Druid_SerpentSpeed"]           = Druid_SerpentSpeed;
                SyncedEntries["vl_svr_Druid_SerpentScale"]           = Druid_SerpentScale;
                SyncedEntries["vl_svr_Druid_RegenDuration"]          = Druid_RegenDuration;
                SyncedEntries["vl_svr_Druid_RegenInterval"]          = Druid_RegenInterval;
                SyncedEntries["vl_svr_Druid_RegenCleansePoison"]     = Druid_RegenCleansePoison;
                // (TrophyLeashDistance y TrophyFollowCheckRate ya estaban registradas con sus claves originales)
                SyncedEntries["vl_svr_Druid_TrophyLeashDistance"]    = Druid_TrophyLeashDistance;
                SyncedEntries["vl_svr_Druid_TrophyFollowCheckRate"]  = Druid_TrophyFollowCheckRate;

                // Priest
                SyncedEntries["vl_svr_Priest_HealOverTime"]          = Priest_HealOverTime;
                SyncedEntries["vl_svr_Priest_HealDuration"]          = Priest_HealDuration;
                SyncedEntries["vl_svr_Priest_HealInterval"]          = Priest_HealInterval;
                SyncedEntries["vl_svr_Priest_HealIntFactor"]         = Priest_HealIntFactor;
                SyncedEntries["vl_svr_Priest_SanctifyHealTotal"]     = Priest_SanctifyHealTotal;
                SyncedEntries["vl_svr_Priest_SanctifyDuration"]      = Priest_SanctifyDuration;
                SyncedEntries["vl_svr_Priest_SanctifyInterval"]      = Priest_SanctifyInterval;
                SyncedEntries["vl_svr_Priest_SanctifyRadius"]        = Priest_SanctifyRadius;
                SyncedEntries["vl_svr_Priest_SanctifyIntFactor"]     = Priest_SanctifyIntFactor;
                SyncedEntries["vl_svr_Priest_PurgeFrostDamage"]      = Priest_PurgeFrostDamage;
                SyncedEntries["vl_svr_Priest_PurgeFrostScale"]       = Priest_PurgeFrostScale;
                SyncedEntries["vl_svr_Priest_PurgeRadius"]           = Priest_PurgeRadius;
                SyncedEntries["vl_svr_Priest_PurgeFreeze"]           = Priest_PurgeFreeze;
                SyncedEntries["vl_svr_Priest_PurgeFreezeDur"]        = Priest_PurgeFreezeDur;
                SyncedEntries["vl_svr_Priest_PurgeFreezeSlow"]       = Priest_PurgeFreezeSlow;
                SyncedEntries["vl_svr_Priest_PurgeBaseCooldown"]     = Priest_PurgeBaseCooldown;
                SyncedEntries["vl_svr_Priest_PurgeCdIntFactor"]      = Priest_PurgeCdIntFactor;
                SyncedEntries["vl_svr_Priest_PurgeCdMin"]            = Priest_PurgeCdMin;
                SyncedEntries["vl_svr_Priest_PurgeRadiusIntFactor"]  = Priest_PurgeRadiusIntFactor;

                // Valkyrie
                SyncedEntries["vl_svr_Valk_TauntEnabled"]            = Valk_TauntEnabled;
                SyncedEntries["vl_svr_Valk_TauntRadius"]             = Valk_TauntRadius;
                SyncedEntries["vl_svr_Valk_TauntDuration"]           = Valk_TauntDuration;
                SyncedEntries["vl_svr_Valk_TauntBulwark"]            = Valk_TauntBulwark;
                SyncedEntries["vl_svr_Valk_TauntStagger"]            = Valk_TauntStagger;
                SyncedEntries["vl_svr_Valk_TauntLeap"]               = Valk_TauntLeap;

                // Pantheon
                SyncedEntries["vl_svr_Pan_MortalWillStacks"]         = Pan_MortalWillStacks;
                SyncedEntries["vl_svr_Pan_MortalWillMult"]           = Pan_MortalWillMult;
                SyncedEntries["vl_svr_Pan_Q_Damage"]                 = Pan_Q_Damage;
                SyncedEntries["vl_svr_Pan_Q_DamageScale"]            = Pan_Q_DamageScale;
                SyncedEntries["vl_svr_Pan_Q_Cost"]                   = Pan_Q_Cost;
                SyncedEntries["vl_svr_Pan_Q_Cooldown"]               = Pan_Q_Cooldown;
                SyncedEntries["vl_svr_Pan_Q_Speed"]                  = Pan_Q_Speed;
                SyncedEntries["vl_svr_Pan_Q_ChargeForce"]            = Pan_Q_ChargeForce;
                SyncedEntries["vl_svr_Pan_Q_ChargeUp"]               = Pan_Q_ChargeUp;
                SyncedEntries["vl_svr_Pan_Q_ChargeRadius"]           = Pan_Q_ChargeRadius;
                SyncedEntries["vl_svr_Pan_Q_StabRange"]              = Pan_Q_StabRange;
                SyncedEntries["vl_svr_Pan_Q_StabAngle"]              = Pan_Q_StabAngle;
                SyncedEntries["vl_svr_Pan_Block_SpearDamage"]        = Pan_Block_SpearDamage;
                SyncedEntries["vl_svr_Pan_Block_SpearStamina"]       = Pan_Block_SpearStamina;
                SyncedEntries["vl_svr_Pan_E_Duration"]               = Pan_E_Duration;
                SyncedEntries["vl_svr_Pan_E_FireDmgPerSec"]          = Pan_E_FireDmgPerSec;
                SyncedEntries["vl_svr_Pan_E_FireDmgPerStrength"]     = Pan_E_FireDmgPerStrength;
                SyncedEntries["vl_svr_Pan_E_Range"]                  = Pan_E_Range;
                SyncedEntries["vl_svr_Pan_E_Angle"]                  = Pan_E_Angle;
                SyncedEntries["vl_svr_Pan_E_Cost"]                   = Pan_E_Cost;
                SyncedEntries["vl_svr_Pan_E_Cooldown"]               = Pan_E_Cooldown;
                SyncedEntries["vl_svr_Pan_R_JumpHeight"]             = Pan_R_JumpHeight;
                SyncedEntries["vl_svr_Pan_R_HoverTime"]              = Pan_R_HoverTime;
                SyncedEntries["vl_svr_Pan_R_AoeRadius"]              = Pan_R_AoeRadius;
                SyncedEntries["vl_svr_Pan_R_ImpactDamage"]           = Pan_R_ImpactDamage;
                SyncedEntries["vl_svr_Pan_R_DamagePerStrength"]      = Pan_R_DamagePerStrength;
                SyncedEntries["vl_svr_Pan_R_StunDuration"]           = Pan_R_StunDuration;
                SyncedEntries["vl_svr_Pan_R_StunPerStrength"]        = Pan_R_StunPerStrength;
                SyncedEntries["vl_svr_Pan_R_Cost"]                   = Pan_R_Cost;
                SyncedEntries["vl_svr_Pan_R_Cooldown"]               = Pan_R_Cooldown;
                SyncedEntries["vl_svr_Pan_Item"]                     = Pan_Item;

                // Hook: si soy CLIENTE conectado a un server, cualquier cambio local
                // se revierte al último valor sincronizado por el server.
                foreach (var kvp in SyncedEntries)
                {
                    string syncKey = kvp.Key;
                    ConfigEntryBase entry = kvp.Value;
                    if (entry == null) continue;
                    entry.SettingChanged += (s, e) =>
                    {
                        try
                        {
                            if (ZNet.instance != null && !ZNet.instance.IsServer()
                                && VL_ConfigSync.ServerValues.TryGetValue(syncKey, out object srvVal)
                                && srvVal != null)
                            {
                                if (!object.Equals(entry.BoxedValue, srvVal))
                                {
                                    entry.BoxedValue = srvVal;
                                    Debug.LogWarning("[VL] Revertido cambio local de " + syncKey + " (server-authoritative).");
                                }
                            }
                        }
                        catch { }
                    };
                }

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

        // Multiplicador de heal del Sanctify (HoT) según Intellect de EpicMMO.
        public static float SanctifyHealIntMult()
        {
            if (Priest_SanctifyIntFactor == null) return 1f;
            int intel = VL_EpicMMOBridge.GetIntellect();
            return 1f + (intel * Priest_SanctifyIntFactor.Value / 100f);
        }

        // Cooldown del Purge escalado por Intellect (más Int = menos cooldown).
        public static float PurgeCooldown()
        {
            float baseCd = Priest_PurgeBaseCooldown != null ? Priest_PurgeBaseCooldown.Value : 15f;
            float factor = Priest_PurgeCdIntFactor != null ? Priest_PurgeCdIntFactor.Value : 0f;
            float min    = Priest_PurgeCdMin != null ? Priest_PurgeCdMin.Value : 4f;
            int intel = VL_EpicMMOBridge.GetIntellect();
            return Mathf.Max(min, baseCd - intel * factor);
        }

        // Radio del Purge escalado por Intellect (más Int = más área).
        public static float PurgeRadius(float skillFallback)
        {
            float baseR = Priest_PurgeRadius != null ? Priest_PurgeRadius.Value : skillFallback;
            float factor = Priest_PurgeRadiusIntFactor != null ? Priest_PurgeRadiusIntFactor.Value : 0f;
            int intel = VL_EpicMMOBridge.GetIntellect();
            return baseR + intel * factor;
        }
    }
}