namespace AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests.Probe;

public sealed class ProbeCounterRepository(IModuleEventStoreSession<IProbeEventStore> session)
    : MartenEventSourcedRepository<ProbeCounter, IProbeEventStore>(session);
