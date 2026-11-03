using UnityEngine;


namespace VRBeats
{
    public class VR_InteractorController : MonoBehaviour
    {
        private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor = null;
        private UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual interactorLineVisual = null;
        private LineRenderer lineRender = null;
        
        private void Awake()
        {
            rayInteractor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
            interactorLineVisual = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>();
            lineRender = GetComponent<LineRenderer>();
                        
            
            DisableXRRayInteractorComponents();
        }
        
        public void DisableXRRayInteractorComponents()
        {
            rayInteractor.enabled = false;
            interactorLineVisual.enabled = false;
            lineRender.enabled = false;
        }

        public void EnableXRRayInteractorComponents()
        {           
            rayInteractor.enabled = true;
            interactorLineVisual.enabled = true;
            lineRender.enabled = true;
        }

    }

}
