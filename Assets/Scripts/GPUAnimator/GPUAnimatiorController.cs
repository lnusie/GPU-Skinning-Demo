﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[ExecuteInEditMode]
public class GPUAnimatiorController : MonoBehaviour
{
    public AnimationInfo[] m_AnimInfos;
    public AnimatorParamInfo m_AnimationParams; //TODO: 修饰符改为private，在Editor赋值
    public AnimatorParamInfo AnimationParams
    {
        get
        { if (m_AnimationParams == null)
            {
                m_AnimationParams = ScriptableObject.CreateInstance<AnimatorParamInfo>();
            }
            return m_AnimationParams;
        }
    }

    public float m_Speed = 1.0f; 

    MaterialPropertyBlock m_MaterialPropertyBlock;
    MeshRenderer m_MeshRenderer;
    AnimationInfo m_CurAnimInfo;
    Dictionary<string, AnimationInfo> m_AnimInfoDict;
    float m_PlayTime0 = 0;
    float m_PlayTime1 = 0;
    float m_BlendFrameOffset = 0;
    float m_BlendFrames = 0;
    bool m_Inited = false;
    AnimationTransition m_TransformInfo;


    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            m_MeshRenderer = GetComponent<MeshRenderer>();
            m_MaterialPropertyBlock = new MaterialPropertyBlock();
            m_MeshRenderer.GetPropertyBlock(m_MaterialPropertyBlock, 0);
            float boundMax = float.MinValue;
            float boundMin = float.MaxValue;
            var meshFilter = GetComponent<MeshFilter>();
            var mesh = meshFilter.sharedMesh;
            foreach (var vertex in mesh.vertices)
            {
                boundMin = Mathf.Min(boundMin, vertex.x, vertex.y, vertex.z);
                boundMax = Mathf.Max(boundMax, vertex.x, vertex.y, vertex.z);
            }
            m_MaterialPropertyBlock.SetFloat("_BoundMax0", boundMax);
            m_MaterialPropertyBlock.SetFloat("_BoundMin0", boundMin);
            m_MaterialPropertyBlock.SetFloat("_BlendFactor", 0);
            m_MeshRenderer.SetPropertyBlock(m_MaterialPropertyBlock, 0);
        }
    }

    private void Start()
    {
        Init();
        PlayDefaultAnimation();
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (!m_CurAnimInfo) return;

        float t = 0;
        if (m_CurAnimInfo.m_Loop)
        {
            t = (m_PlayTime0 % m_CurAnimInfo.m_AnimLength) / m_CurAnimInfo.m_AnimLength;//当前动画播到了第几帧
        }
        else
        {
            t = m_PlayTime0 / m_CurAnimInfo.m_AnimLength;
            t = Mathf.Min(1, t);
        }
        float frameNumber = t * m_CurAnimInfo.m_AnimFrameNum;

        float index = (frameNumber + m_CurAnimInfo.m_AnimFrameOffset);//定位到贴图中的Y坐标
        float curFrameIndex = (index / m_CurAnimInfo.m_TotalFrames);//将Y坐标映射进01区间
        m_PlayTime0 += (Time.deltaTime * m_Speed);
        m_MaterialPropertyBlock.SetFloat("_FrameIndex0", curFrameIndex);
        
        if (m_TransformInfo == null)
        {
            foreach (var transfomInfo in m_CurAnimInfo.m_AnimationTransformInfos)
            {
                bool allMatch = true;
                if (transfomInfo.m_Conditions != null)
                {
                    foreach (var condition in transfomInfo.m_Conditions)
                    {
                        if (!MatchCondition(condition))
                        {
                            allMatch = false;
                        }
                        else
                        {
                            OnMatchsCondition(condition);
                        }
                    }
                }
                if (allMatch)
                {
                    m_TransformInfo = transfomInfo;
                    m_BlendFrameOffset = frameNumber - m_TransformInfo.m_StartFrame;
                    m_BlendFrameOffset = Mathf.Max(0, m_BlendFrameOffset);
                    m_MaterialPropertyBlock.SetFloat("_BoundMax1", m_TransformInfo.m_AnimInfo1.m_boundMax);
                    m_MaterialPropertyBlock.SetFloat("_BoundMin1", m_TransformInfo.m_AnimInfo1.m_boundMin);
                    m_PlayTime1 = 0;
                    break;
                }
            }
        }
        float blendFactor = 0;
        if (m_TransformInfo != null)
        {
            var animInfo1 = m_TransformInfo.m_AnimInfo1;
            if (frameNumber >= m_TransformInfo.m_StartFrame) //开始混入动画2
            {
                Debug.LogError("混合动画2 " + m_TransformInfo.m_AnimInfo1.m_AnimName);
                float t1 = m_PlayTime1 / animInfo1.m_AnimLength;
                float frameNumber1 = t1 * animInfo1.m_AnimFrameNum + m_BlendFrameOffset;
                float index1 = (frameNumber1 + animInfo1.m_AnimFrameOffset); 
                float frameIndex1 = (index1 / animInfo1.m_TotalFrames);
                m_MaterialPropertyBlock.SetFloat("_FrameIndex1", frameIndex1);
                m_PlayTime1 += Time.deltaTime * m_Speed;
            }
            blendFactor = (frameNumber - m_TransformInfo.m_StartFrame) / m_TransformInfo.m_BlendFrame;
            blendFactor = Mathf.Min(1, blendFactor);
            blendFactor = Mathf.Max(0, blendFactor);
            Debug.LogError("blendFactor >>  "+ blendFactor);

            if (Mathf.Abs(blendFactor - 1) < 0.01f) //混合结束,将动画2设置成当前动画
            {
                Debug.LogError("混合结束,将动画2设置成当前动画 !");
                Play(m_TransformInfo.m_AnimInfo1);
                m_TransformInfo = null;
                m_PlayTime0 = m_PlayTime1;
            }
        }
        m_MaterialPropertyBlock.SetFloat("_BlendFactor", blendFactor);
        m_MeshRenderer.SetPropertyBlock(m_MaterialPropertyBlock, 0);


        //if (m_TransformInfo == null)
        //{
        //    float t = 0;
        //    if (m_CurAnimInfo.m_Loop)
        //    {
        //        t = (m_PlayTime0 % m_CurAnimInfo.m_AnimLength) / m_CurAnimInfo.m_AnimLength;//当前动画播到了第几帧
        //    }
        //    else
        //    {
        //        t = m_PlayTime0 / m_CurAnimInfo.m_AnimLength;
        //        t = Mathf.Min(1, t);
        //    }
        //    float index = (t * m_CurAnimInfo.m_AnimFrameNum + m_CurAnimInfo.m_AnimFrameOffset);//定位到贴图中的Y坐标
        //    float curFrameIndex = (index / m_CurAnimInfo.m_TotalFrames);//将Y坐标映射进01区间
        //    m_PlayTime0 += (Time.deltaTime * m_Speed);
        //    m_MaterialPropertyBlock.SetFloat("_FrameIndex0", curFrameIndex);
        //    m_MeshRenderer.SetPropertyBlock(m_MaterialPropertyBlock, 0);
        //}
        //else
        //{
        //    var animInfo0 = m_TransformInfo.m_AnimInfo0;
        //    var animInfo1 = m_TransformInfo.m_AnimInfo1;

        //    float t0 = m_PlayTime0 / animInfo0.m_AnimLength;//当前动画播到了第几帧
        //    float frameNumber = t0 * animInfo0.m_AnimFrameNum;
        //    float index0 = (frameNumber + animInfo0.m_AnimFrameOffset);
        //    float frameIndex0 = (index0 / animInfo0.m_TotalFrames);//将Y坐标映射进01区间
        //    m_MaterialPropertyBlock.SetFloat("_FrameIndex0", frameIndex0);
        //    float blendFactor = 0;
        //    if (frameNumber >= m_TransformInfo.m_StartFrame) //开始混入第二动画
        //    {
        //        float t1 = m_PlayTime1 / animInfo1.m_AnimLength;
        //        float index1 = (t1 * animInfo1.m_AnimFrameNum + animInfo1.m_AnimFrameOffset);
        //        float frameIndex1 = (index1 / animInfo1.m_TotalFrames);
        //        m_MaterialPropertyBlock.SetFloat("_FrameIndex1", frameIndex1);
        //        m_PlayTime1 += Time.deltaTime * m_Speed;
        //    }
        //    blendFactor = (frameNumber - m_TransformInfo.m_StartFrame) / m_TransformInfo.m_BlendFrame;
        //    blendFactor = Mathf.Min(1, blendFactor);
        //    blendFactor = Mathf.Max(0, blendFactor);

        //    m_PlayTime0 += Time.deltaTime * m_Speed;
        //    m_MaterialPropertyBlock.SetFloat("_BlendFactor", blendFactor);
        //    m_MeshRenderer.SetPropertyBlock(m_MaterialPropertyBlock, 0);
        //    if (m_PlayTime1 > animInfo1.m_AnimLength) //混合结束
        //    {
        //        m_TransformInfo = null;
        //        var playTime = m_PlayTime0;
        //        Play(animInfo1.m_AnimName);
        //        if (!animInfo1.m_Loop)
        //        {
        //            m_PlayTime0 = playTime; 
        //        }
        //    }
        //}
    }

    void Init()
    {
        if (!m_Inited)
        {
            m_MeshRenderer = GetComponent<MeshRenderer>();
            m_MaterialPropertyBlock = new MaterialPropertyBlock();
            m_MeshRenderer.GetPropertyBlock(m_MaterialPropertyBlock, 0);
            m_Inited = true;
        }
    }

    public void Play(AnimationTransition transformInfo)
    {
        m_MaterialPropertyBlock.SetFloat("_BoundMax0", transformInfo.m_AnimInfo0.m_boundMax);
        m_MaterialPropertyBlock.SetFloat("_BoundMin0", transformInfo.m_AnimInfo0.m_boundMin);

        m_MaterialPropertyBlock.SetFloat("_BoundMax1", transformInfo.m_AnimInfo1.m_boundMax);
        m_MaterialPropertyBlock.SetFloat("_BoundMin1", transformInfo.m_AnimInfo1.m_boundMin);

        m_MeshRenderer.SetPropertyBlock(m_MaterialPropertyBlock, 0);

        m_TransformInfo = transformInfo;
        m_CurAnimInfo = null;
        m_PlayTime0 = 0;
        m_PlayTime1 = 0;
    }

    public void Play(string animName)
    {
        if(m_CurAnimInfo != null && m_CurAnimInfo.m_AnimName == animName)
        {
            return;
        }

        AnimationInfo animInfo = GetAnimInfo(animName);
        if(animInfo == null)
        {
            return;
        }
        Play(animInfo);
    }

    public void Play(AnimationInfo info)
    {
        m_CurAnimInfo = info;
        m_MaterialPropertyBlock.SetFloat("_BoundMax0", info.m_boundMax);
        m_MaterialPropertyBlock.SetFloat("_BoundMin0", info.m_boundMin);
        m_MaterialPropertyBlock.SetFloat("_BlendFactor", 0);
        m_MeshRenderer.SetPropertyBlock(m_MaterialPropertyBlock, 0);
        m_PlayTime0 = 0;
    }

    AnimationInfo GetAnimInfo(string animName)
    {
        if(m_AnimInfoDict == null)
        {
            m_AnimInfoDict = new Dictionary<string, AnimationInfo>();
            for (int i = 0; i < m_AnimInfos.Length; i++)
            {
                m_AnimInfoDict.Add(m_AnimInfos[i].m_AnimName, m_AnimInfos[i]);
            }
        }

        AnimationInfo animInfo = null;
        m_AnimInfoDict.TryGetValue(animName, out animInfo);
        return animInfo;
    }

    void PlayDefaultAnimation()
    {
        for (int i = 0; i < m_AnimInfos.Length; i++)
        {
            if(m_AnimInfos[i].m_IsDefault)
            {
                Play(m_AnimInfos[i].m_AnimName);
                break;
            }
        }
    }

    private bool MatchCondition(AnimationTransitionCondition condition)
    {
        bool result = false;
        switch (condition.m_Type)
        {
            case AnimationTransitionConditionType.Trigger:
                bool triggerValue;
                bool haveDefine = AnimationParams.GetTriggerValue(condition.m_ParamName, out triggerValue);
                result = haveDefine && triggerValue;
                break;
            case AnimationTransitionConditionType.Bool:
                //TODO
                break;
            case AnimationTransitionConditionType.Int:
                //TODO
                break;
            case AnimationTransitionConditionType.Float:
                //TODO
                break;
        }
        return result;
    }

    private void OnMatchsCondition(AnimationTransitionCondition condition)
    {
        switch (condition.m_Type)
        {
            case AnimationTransitionConditionType.Trigger:
                AnimationParams.SetTriggerValue(condition.m_ParamName, false);
                break;
            case AnimationTransitionConditionType.Bool:
                break;
            case AnimationTransitionConditionType.Int:
                break;
            case AnimationTransitionConditionType.Float:
                break;
        }
    }

    public void SetTrigger(string triggerName)
    {
        AnimationParams.SetTriggerValue(triggerName, true);
    }


}
