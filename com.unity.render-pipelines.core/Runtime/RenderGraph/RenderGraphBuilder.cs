using System;
using System.Collections.Generic;

namespace UnityEngine.Experimental.Rendering.RenderGraphModule
{
    /// <summary>
    /// The RenderGraphBuilder is the class that allows users to setup a new Render Pass
    /// </summary>
    public struct RenderGraphBuilder : IDisposable
    {
        RenderGraph.RenderPass      m_RenderPass;
        RenderGraphResourceRegistry m_Resources;
        bool                        m_Disposed;

        #region Public Interface
        /// <summary>
        /// Specify that a texture resource will be used as a color render target during the pass
        /// This will have the same effect as WriteTexture and will also automatically set the texture as a render target
        /// </summary>
        /// <param name="input">Texture resource that will be used as a color render target</param>
        /// <param name="index">Index for multiple render target usage</param>
        /// <returns>An updated resource handle to the input resource</returns>
        public RenderGraphMutableResource UseColorBuffer(in RenderGraphMutableResource input, int index)
        {
            if (input.type != RenderGraphResourceType.Texture)
                throw new ArgumentException("Trying to write to a resource that is not a texture or is invalid.");

            m_RenderPass.SetColorBuffer(input, index);
            m_Resources.UpdateTextureFirstWrite(input, m_RenderPass.index);
            return input;
        }

        /// <summary>
        /// Specify that a texture resource will be used a depth buffer during the pass
        /// </summary>
        /// <param name="input">Texture resource that will be used as a depth buffer</param>
        /// <param name="flags">Specify if the depth buffer will be read, written to, or both</param>
        /// <returns>An updated resource handle to the input resource</returns>
        public RenderGraphMutableResource UseDepthBuffer(in RenderGraphMutableResource input, DepthAccess flags)
        {
            if (input.type != RenderGraphResourceType.Texture)
                throw new ArgumentException("Trying to write to a resource that is not a texture or is invalid.");

            m_RenderPass.SetDepthBuffer(input, flags);
            if ((flags | DepthAccess.Read) != 0)
                m_Resources.UpdateTextureLastRead(input, m_RenderPass.index);
            if ((flags | DepthAccess.Write) != 0)
                m_Resources.UpdateTextureFirstWrite(input, m_RenderPass.index);
            return input;
        }

        /// <summary>
        /// Specify that a texture resource will be read during the pass
        /// </summary>
        /// <param name="input">Texture resource that will be read during the pass</param>
        /// <returns>An updated resource handle to the input resource</returns>
        public RenderGraphResource ReadTexture(in RenderGraphResource input)
        {
            if (input.type != RenderGraphResourceType.Texture)
                throw new ArgumentException("Trying to read a resource that is not a texture or is invalid.");
            m_RenderPass.resourceReadList.Add(input);
            m_Resources.UpdateTextureLastRead(input, m_RenderPass.index);
            return input;
        }

        /// <summary>
        /// Specify that a texture resource will be written to during the pass
        /// </summary>
        /// <param name="input">Texture resource that will be written to during the pass</param>
        /// <returns>An updated resource handle to the input resource</returns>
        public RenderGraphMutableResource WriteTexture(in RenderGraphMutableResource input)
        {
            if (input.type != RenderGraphResourceType.Texture)
                throw new ArgumentException("Trying to write to a resource that is not a texture or is invalid.");
            // TODO: Manage resource "version" for debugging purpose
            m_RenderPass.resourceWriteList.Add(input);
            m_Resources.UpdateTextureFirstWrite(input, m_RenderPass.index);
            return input;
        }

        /// <summary>
        /// Specify that a renderer list resource will be used during this pass
        /// </summary>
        /// <param name="input">Renderer List resource that will be used during the pass</param>
        /// <returns>An updated resource handle to the input resource</returns>
        public RenderGraphResource UseRendererList(in RenderGraphResource input)
        {
            if (input.type != RenderGraphResourceType.RendererList)
                throw new ArgumentException("Trying use a resource that is not a renderer list.");
            m_RenderPass.usedRendererListList.Add(input);
            return input;
        }

        /// <summary>
        /// Specify the render function used for this pass
        /// A call to this is mandatory for the pass to be valid
        /// </summary>
        /// <typeparam name="PassData">Type of the class used to provide data to the Render Pass</typeparam>
        /// <param name="renderFunc">Render function for the pass</param>
        public void SetRenderFunc<PassData>(RenderFunc<PassData> renderFunc) where PassData : class, new()
        {
            ((RenderGraph.RenderPass<PassData>)m_RenderPass).renderFunc = renderFunc;
        }

        /// <summary>
        /// Enable asynchronous compute for this pass
        /// </summary>
        /// <param name="value">Set to true to enable asynchronous compute</param>
        public void EnableAsyncCompute(bool value)
        {
            m_RenderPass.enableAsyncCompute = value;
        }

        /// <summary>
        /// Dispose the RenderGraphBuilder instance
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

        #region Internal Interface
        internal RenderGraphBuilder(RenderGraph.RenderPass renderPass, RenderGraphResourceRegistry resources)
        {
            m_RenderPass = renderPass;
            m_Resources = resources;
            m_Disposed = false;
        }

        void Dispose(bool disposing)
        {
            if (m_Disposed)
                return;

            m_Disposed = true;
        }
        #endregion
    }
}
