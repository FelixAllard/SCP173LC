using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;

namespace SCP173.Configuration {
    public class PluginConfig
    {
        // For more info on custom configs, see https://lethal.wiki/dev/intermediate/custom-configs
        public ConfigEntry<int> SpawnWeight;
        public ConfigEntry<int> TicksAfterKilling;
        public ConfigEntry<int> AmmountOfDamage;
        public ConfigEntry<float> LeapDistance;
        public ConfigEntry<int> NumberOfLeapPerFrame;
        public ConfigEntry<int> AmmountOfTimeWaiting;
        public PluginConfig(ConfigFile cfg)
        {
            SpawnWeight = cfg.Bind("General", "Spawn weight", 25,
                "The spawn chance weight for SCP173, relative to other existing enemies.\n" +
                "Goes up from 0, lower is more rare, 100 and up is very common.");
            TicksAfterKilling = cfg.Bind("Behaviour", "Frame after killing", 1000,
                "How much time in frames will SCP 173 stop moving and doing anything after killing a player");
            AmmountOfDamage = cfg.Bind("Behaviour", "Amount of damage he does on contact", 100,
                "At 100, he will 1 shot you. Put it to less if you want him to be way more forgiving");
            LeapDistance = cfg.Bind("Performance", "Distance for each TP", 1f,
                "SCP 173 works by teleporting a small distance at a super fast speed, each time detecting if he is seen in his new position. The smaller the interval, the more precise SCP173 will be in his teleport, but this also mean that he won't kill you on the same frame you looked away if to small");
            NumberOfLeapPerFrame = cfg.Bind("Performance", "Number Of TP Iteration per frame", 100,
                "Multiply this value by the Distance for each TP value to get the maximum distance he can do per frame. Do note that this value can affect performance if set to high. Putting it to low will lead to slower speed.");
            AmmountOfTimeWaiting = cfg.Bind("Behaviour", "Number Of TP Iteration per frame", 1500,
                "SCP 173 works by teleporting around the facility, waiting a few frame for someone to see it, and then teleporting somewhere else. This value represent how much time will he wait at the same location in frame");
            
            ClearUnusedEntries(cfg);
        }

        private void ClearUnusedEntries(ConfigFile cfg) {
            // Normally, old unused config entries don't get removed, so we do it with this piece of code. Credit to Kittenji.
            PropertyInfo orphanedEntriesProp = cfg.GetType().GetProperty("OrphanedEntries", BindingFlags.NonPublic | BindingFlags.Instance);
            var orphanedEntries = (Dictionary<ConfigDefinition, string>)orphanedEntriesProp.GetValue(cfg, null);
            orphanedEntries.Clear(); // Clear orphaned entries (Unbinded/Abandoned entries)
            cfg.Save(); // Save the config file to save these changes
        }
    }
}