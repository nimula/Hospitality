using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Hospitality
{
    public class Hospitality_MapComponent : MapComponent
    {
        public static Hospitality_MapComponent Instance(Map map)
        {
            {
                return map.GetComponent<Hospitality_MapComponent>() ?? new Hospitality_MapComponent(true, map);
            }
        }

        private IncidentQueue incidentQueue = new IncidentQueue();
        public bool defaultEntertain;
        public bool defaultMakeFriends;
        public Area defaultAreaRestriction;
        public Area defaultAreaShopping;
        public bool refuseGuestsUntilWeHaveBeds;
        private int nextQueueInspection;
        private int nextRogueGuestCheck;

        [Obsolete]
        private PrisonerInteractionModeDef defaultInteractionMode; // Not used anymore, only for legacy saves
        [Obsolete]
        private int lastEventKey;
        [Obsolete]
        private Dictionary<int, int> bribeCount = new Dictionary<int, int>(); // uses faction.randomKey

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref bribeCount, "bribeCount", LookMode.Value, LookMode.Value);
            Scribe_Defs.Look(ref defaultInteractionMode, "defaultInteractionMode");
            Scribe_Values.Look(ref defaultEntertain, "defaultEntertain");
            Scribe_Values.Look(ref defaultMakeFriends, "defaultMakeFriends");
            Scribe_References.Look(ref defaultAreaRestriction, "defaultAreaRestriction");
            Scribe_References.Look(ref defaultAreaShopping, "defaultAreaShopping");
            Scribe_Values.Look(ref lastEventKey, "lastEventKey");
            Scribe_Deep.Look(ref incidentQueue, "incidentQueue");
            Scribe_Values.Look(ref refuseGuestsUntilWeHaveBeds, "refuseGuestsUntilWeHaveBeds");
            Scribe_Values.Look(ref nextQueueInspection, "nextQueueInspection");

            if (defaultAreaRestriction == null) defaultAreaRestriction = map.areaManager.Home;

            // For legacy:
            if (defaultInteractionMode == PrisonerInteractionModeDefOf.ReduceResistance)
            {
                defaultEntertain = true;
                defaultInteractionMode = PrisonerInteractionModeDefOf.NoInteraction;
            }
        }

        public Hospitality_MapComponent(Map map) : base(map)
        {
        }

        public Hospitality_MapComponent(bool forReal, Map map) : base(map)
        {
            // Multi-Threading killed the elegant solution
            if (!forReal) return;
            map.components.Add(this);
            defaultAreaRestriction = map.areaManager.Home;
            defaultInteractionMode = PrisonerInteractionModeDefOf.NoInteraction;
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            if (incidentQueue == null) incidentQueue = new IncidentQueue();
            if(incidentQueue.Count <= 1) GenericUtility.FillIncidentQueue(map);
            incidentQueue.IncidentQueueTick();

            if (GenTicks.TicksGame > nextQueueInspection)
            {
                nextQueueInspection = GenTicks.TicksGame + GenDate.TicksPerDay;
                GenericUtility.CheckTooManyIncidentsAtOnce(incidentQueue);
            }

            if (GenTicks.TicksGame > nextRogueGuestCheck)
            {
                nextRogueGuestCheck = GenTicks.TicksGame + GenDate.TicksPerHour;
                GuestUtility.CheckForRogueGuests(map);
            }
        }

        public void QueueIncident(FiringIncident incident, float afterDays)
        {
            var qi = new QueuedIncident(incident, (int)(Find.TickManager.TicksGame + GenDate.TicksPerDay * afterDays));
            incidentQueue.Add(qi);
            //Log.Message("Queued Hospitality incident after " + afterDays + " days. Queue has now " + incidentQueue.Count + " items.");
        }

        public QueuedIncident GetNextVisit(Faction faction)
        {
            QueuedIncident nearest = null;

            // Find earliest
            foreach (QueuedIncident incident in incidentQueue)
            {
                if (incident.FiringIncident.parms.faction == faction)
                {
                    if (nearest == null || incident.FireTick < nearest.FireTick) nearest = incident;
                }
            }
            return nearest;
        }

        [Obsolete]
        public int GetBribeCount(Faction faction)
        {
            if (faction == null) throw new NullReferenceException("Faction not set.");
            int result;
            if (bribeCount.TryGetValue(faction.randomKey, out result)) return result;

            return 0;
        }

        [Obsolete]
        public void Bribe(Faction faction)
        {
            if (faction == null) throw new NullReferenceException("Faction not set.");

            bribeCount[faction.randomKey] = GetBribeCount(faction) + 1;
        }

        public static void RefuseGuestsUntilWeHaveBeds(Map map)
        {
            if (map == null) return;

            var mapComp = Instance(map);
            mapComp.refuseGuestsUntilWeHaveBeds = true;
            LessonAutoActivator.TeachOpportunity(ConceptDef.Named("GuestBeds"), null, OpportunityType.Important);
        }
    }
}
