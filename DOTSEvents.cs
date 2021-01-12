using System;
using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;

public class FireEventsOnNextFrame<T> where T : struct, IComponentData 
{

    private World _world;
    private EndSimulationEntityCommandBufferSystem _endSimulationEntityCommandBufferSystem;
    private EntityManager _entityManager;
    private EntityArchetype _eventEntityArchetype;
    private EntityQuery _eventEntityQuery;
    private Action<T> _OnEventAction;

    private EventTrigger _eventCaller;
    private EntityCommandBuffer _entityCommandBuffer;

    public FireEventsOnNextFrame(World world, Action<T> OnEventAction = null) 
    {
        _world = world;
        _OnEventAction = OnEventAction;
        _endSimulationEntityCommandBufferSystem = world.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        _entityManager = world.EntityManager;

        _eventEntityArchetype = _entityManager.CreateArchetype(typeof(T));
        _eventEntityQuery = _entityManager.CreateEntityQuery(typeof(T));
    }

    public EventTrigger GetEventTrigger() 
    {
        _entityCommandBuffer = _endSimulationEntityCommandBufferSystem.CreateCommandBuffer();
        _eventCaller = new EventTrigger(_eventEntityArchetype, _entityCommandBuffer);
        return _eventCaller;
    }

    public void CaptureEvents(JobHandle jobHandleWhereEventsWereScheduled, Action<T> OnEventAction = null) 
    {
        _endSimulationEntityCommandBufferSystem.AddJobHandleForProducer(jobHandleWhereEventsWereScheduled);
        _eventCaller.Playback(_endSimulationEntityCommandBufferSystem.CreateCommandBuffer(), 
            _eventEntityQuery, OnEventAction == null ? _OnEventAction : OnEventAction);
    }

    public struct EventTrigger 
    {
        private struct EventJob : IJobForEachWithEntity<T> 
        {
            public EntityCommandBuffer.Concurrent entityCommandBufferConcurrent;
            public NativeList<T> nativeList;

            public void Execute(Entity entity, int index, ref T c0) 
            {
                nativeList.Add(c0);
                entityCommandBufferConcurrent.DestroyEntity(index, entity);
            }
        }

        private EntityCommandBuffer.Concurrent _entityCommandBufferConcurrent;
        private EntityArchetype _entityArchetype;

        public EventTrigger(EntityArchetype entityArchetype, EntityCommandBuffer entityCommandBuffer) 
        {
            _entityArchetype = entityArchetype;
            _entityCommandBufferConcurrent = entityCommandBuffer.ToConcurrent();
        }

        public void TriggerEvent(int entityInQueryIndex) 
        {
            _entityCommandBufferConcurrent.CreateEntity(entityInQueryIndex, _entityArchetype);
        }

        public void TriggerEvent(int entityInQueryIndex, T t) 
        {
            var entity = _entityCommandBufferConcurrent.CreateEntity(entityInQueryIndex, _entityArchetype);
            _entityCommandBufferConcurrent.SetComponent(entityInQueryIndex, entity, t);
        }

        public void Playback(EntityCommandBuffer destroyEntityCommandBuffer, EntityQuery eventEntityQuery, Action<T> OnEventAction) 
        {
            if (eventEntityQuery.CalculateEntityCount() > 0) 
            {
                var nativeList = new NativeList<T>(Allocator.TempJob);
            
                new EventJob 
                {
                    entityCommandBufferConcurrent = destroyEntityCommandBuffer.ToConcurrent(),
                    nativeList = nativeList,
                }.Run(eventEntityQuery);

                foreach (T t in nativeList) 
                {
                    OnEventAction(t);
                }

                nativeList.Dispose();
            }
        }

    }

}

public class FireEventsOnSameFrame<T> where T : struct, IComponentData 
{
    private World _world;
    private EntityManager _entityManager;
    private EntityArchetype _eventEntityArchetype;
    private EntityQuery _eventEntityQuery;
    private Action<T> _OnEventAction;

    private EventTrigger eventCaller;
    private EntityCommandBuffer entityCommandBuffer;

    public FireEventsOnSameFrame(World world, Action<T> OnEventAction = null) 
    {
        _world = world;
        _OnEventAction = OnEventAction;
        _entityManager = world.EntityManager;

        _eventEntityArchetype = _entityManager.CreateArchetype(typeof(T));
        _eventEntityQuery = _entityManager.CreateEntityQuery(typeof(T));
    }

    public EventTrigger GetEventTrigger() 
    {
        eventCaller = new EventTrigger(_eventEntityArchetype, out entityCommandBuffer);
        return eventCaller;
    }

    public void CaptureEvents(JobHandle jobHandleWhereEventsWereScheduled, Action<T> OnEventAction = null) 
    {
        eventCaller.Playback(jobHandleWhereEventsWereScheduled, entityCommandBuffer, _entityManager, _eventEntityQuery, 
            OnEventAction == null ? _OnEventAction : OnEventAction);
    }

    public struct EventTrigger 
    {
        private EntityCommandBuffer.Concurrent _entityCommandBufferConcurrent;
        private EntityArchetype _entityArchetype;

        public EventTrigger(EntityArchetype entityArchetype, out EntityCommandBuffer entityCommandBuffer) 
        {
            _entityArchetype = entityArchetype;
            entityCommandBuffer = new EntityCommandBuffer(Allocator.TempJob);
            _entityCommandBufferConcurrent = entityCommandBuffer.ToConcurrent();
        }

        public void TriggerEvent(int entityInQueryIndex) 
        {
            _entityCommandBufferConcurrent.CreateEntity(entityInQueryIndex, _entityArchetype);
        }

        public void TriggerEvent(int entityInQueryIndex, T t) 
        {
            var entity = _entityCommandBufferConcurrent.CreateEntity(entityInQueryIndex, _entityArchetype);
            _entityCommandBufferConcurrent.SetComponent(entityInQueryIndex, entity, t);
        }


        public void Playback(JobHandle jobHandleWhereEventsWereScheduled, EntityCommandBuffer entityCommandBuffer, 
                EntityManager EntityManager, EntityQuery eventEntityQuery, Action<T> OnEventAction) 
        {
            jobHandleWhereEventsWereScheduled.Complete();
            entityCommandBuffer.Playback(EntityManager);
            entityCommandBuffer.Dispose();

            var entityCount = eventEntityQuery.CalculateEntityCount();
            if (entityCount > 0) {
                NativeArray<T> nativeArray = eventEntityQuery.ToComponentDataArray<T>(Allocator.TempJob);
                foreach (T t in nativeArray) {
                    OnEventAction(t);
                }
                nativeArray.Dispose();
            }

            EntityManager.DestroyEntity(eventEntityQuery);
        }

    }

}
