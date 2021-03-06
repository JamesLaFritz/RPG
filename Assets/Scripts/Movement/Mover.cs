// Mover.cs
// 06-30-2022
// James LaFritz

using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RPGEngine.Attributes;
using RPGEngine.Core;
using RPGEngine.Saving;
using UnityEngine;
using UnityEngine.AI;
using static RPGEngine.Core.StringReferences;

namespace RPGEngine.Movement
{
    /// <summary>
    /// A <a href="https://docs.unity3d.com/ScriptReference/MonoBehaviour.html">UnityEngine.MonoBehavior</a> that
    /// uses a <a href="https://docs.unity3d.com/2021.3/Documentation/ScriptReference/AI.NavMeshAgent.html">UnityEngine.AI.NaveMeshAgent</a>
    /// to move a game object to a targets position.
    /// <p>
    /// Implements
    /// <see cref="IAction"/>
    /// <see cref="ISavable"/>
    /// </p>
    /// <p>
    /// <a href="https://docs.unity3d.com/ScriptReference/RequireComponent.html">UnityEngine.RequireComponent</a>(
    /// typeof(<a href="https://docs.unity3d.com/2021.3/Documentation/ScriptReference/AI.NavMeshAgent.html">UnityEngine.AI.NaveMeshAgent</a>)
    /// , typeof(<see cref="ActionScheduler"/>)
    /// , typeof(<see cref="Health"/>)
    /// )</p>
    /// <seealso href="https://docs.unity3d.com/ScriptReference/MonoBehaviour.html"/>
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(ActionScheduler), typeof(Health))]
    public class Mover : MonoBehaviour, IAction, ISavable
    {
        #region Component References

        #region Required

        /// <value>Cache the <see cref="ActionScheduler"/></value>
        private ActionScheduler m_actionScheduler;

        /// <value>Cache the <see cref="Health"/></value>
        private Health m_health;

        #endregion

        #region Optional

        /// <value>Cache the <a href="https://docs.unity3d.com/ScriptReference/AI.NavMeshAgent.html">UnityEngine.AI.NaveMeshAgent</a></value>
        private NavMeshAgent m_navMeshAgent;

        private bool m_hasAgent;

        /// <value>Cache the <a href="https://docs.unity3d.com/ScriptReference/Animator.html">UnityEngine.Animator</a></value>
        private Animator m_animator;

        private bool m_hasAnimator;
        private static int _forwardSpeed;

        #endregion

        #endregion

        #region Unity Messages

        /// <summary>
        /// <seealso href="https://docs.unity3d.com/ScriptReference/MonoBehaviour.Awake.html"/>
        /// </summary>
        private void Awake()
        {
            m_animator = GetComponentInChildren<Animator>();
            m_hasAnimator = m_animator != null;

            if (m_hasAnimator)
            {
                _forwardSpeed = Animator.StringToHash(forwardSpeedFloat);
            }

            m_navMeshAgent = GetComponent<NavMeshAgent>();
            m_hasAgent = m_navMeshAgent != null;

            m_actionScheduler = GetComponent<ActionScheduler>();

            m_health = GetComponent<Health>();
        }

        /// <summary>
        /// <seealso href="https://docs.unity3d.com/ScriptReference/MonoBehaviour.Update.html"/>
        /// </summary>
        private void Update()
        {
            if (m_navMeshAgent.isActiveAndEnabled && m_health.IsDead)
            {
                m_navMeshAgent.destination = transform.position;
                m_navMeshAgent.ResetPath();
                m_navMeshAgent.velocity = Vector3.zero;
                m_navMeshAgent.isStopped = true;
                UpdateAnimator();
                m_navMeshAgent.enabled = false;
            }

            if (m_health.IsDead) return;

            UpdateAnimator();
        }

        #endregion

        #region Implementation of IAction

        /// <inheritdoc />
        public void Cancel()
        {
            if (m_hasAgent)
            {
                m_navMeshAgent.isStopped = true;
            }
        }

        #endregion

        #region Saving and Loading

        [System.Serializable]
        struct MoverSaveData
        {
            public SerializableVector3 position;
            public SerializableVector3 rotation;
        }

        struct MoverLoadData
        {
            public Vector3 position;
            public Vector3 rotation;
        }

        #region Implementation of IJsonSavable

        /// <inheritdoc />
        public JToken CaptureAsJToken()
        {
            JObject state = new JObject();
            IDictionary<string, JToken> stateDict = state;
            stateDict["Position"] = transform.position.ToToken();
            stateDict["Rotation"] = transform.eulerAngles.ToToken();
            return state;
        }

        /// <inheritdoc />
        public void RestoreFromJToken(JToken state, int version)
        {
            if (state == null || version < 4) return;
            if (m_hasAgent) m_navMeshAgent.enabled = false;

            MoverLoadData data = new MoverLoadData();
            switch (version)
            {
                case 4:
                {
                    MoverSaveData sd = state.ToObject<MoverSaveData>();
                    data.position = sd.position.ToVector();
                    data.rotation = sd.rotation.ToVector();
                    break;
                }
                case > 4:
                    data = new MoverLoadData()
                    {
                        position = state.ToObject<MoverLoadData>().position,
                        rotation = state.ToObject<MoverLoadData>().rotation
                    };
                    break;
            }

            Transform transform1 = transform;
            transform1.position = data.position;
            transform1.eulerAngles = data.rotation;

            if (m_hasAgent) m_navMeshAgent.enabled = true;
        }

        #endregion

        #endregion

        #region Public Methods

        /// <summary>
        /// Cancel any previous Actions and Move to the destination.
        /// </summary>
        /// <param name="destination"><a href="https://docs.unity3d.com/2021.3/Documentation/ScriptReference/Vector3.html">UnityEngine.Vector3</a> To move To</param>
        public void StartMoveAction(Vector3 destination)
        {
            m_actionScheduler.StartAction(this);
            MoveTo(destination);
        }

        /// <summary>
        /// Move to the destination.
        /// </summary>
        /// <param name="destination"><a href="https://docs.unity3d.com/2021.3/Documentation/ScriptReference/Vector3.html">UnityEngine.Vector3</a> To move To</param>
        public void MoveTo(Vector3 destination)
        {
            if (m_hasAgent)
            {
                MoveNavMeshAgentTo(destination);
            }
        }

        public void SetMoveSpeed(float speed)
        {
            if (!m_hasAgent) return;
            m_navMeshAgent.speed = speed;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Tell the <a href="https://docs.unity3d.com/2021.3/Documentation/ScriptReference/AI.NavMeshAgent.html">UnityEngine.AI.NaveMeshAgent</a>
        /// to move to the destination.
        /// </summary>
        /// <param name="destination"><a href="https://docs.unity3d.com/2021.3/Documentation/ScriptReference/Vector3.html">UnityEngine.Vector3</a> To move To</param>
        private void MoveNavMeshAgentTo(Vector3 destination)
        {
            if (!m_hasAgent) return;
            m_navMeshAgent!.destination = destination;
            m_navMeshAgent!.isStopped = false;
        }

        /// <summary>
        /// If there is an <a href="https://docs.unity3d.com/ScriptReference/Animator.html">UnityEngine.Animator</a>
        /// then set the "ForwardSpeed" of the Animator to the local velocity on the z access of the navmesh agent.
        /// </summary>
        private void UpdateAnimator()
        {
            if (!m_hasAnimator) return;

            Vector3 velocity = m_navMeshAgent.velocity;
            Vector3 localVelocity = transform.InverseTransformDirection(velocity);
            float speed = localVelocity.z;

            m_animator.SetFloat(_forwardSpeed, speed);
        }

        #endregion
    }
}