using System;
using UnityEngine;
using System.Collections.Generic;

namespace Restock
{
    public class ModuleRestockHeatEffects : PartModule
    {
        // enable the heat glow emissive
        [KSPField] public bool enableHeatEmissive = false;

        // what shader property to modify. must be a color.
        [KSPField] public string shaderProperty = "_EmissiveColor";

        // animation curve for the red channel
        [KSPField] public FloatCurve redCurve = new FloatCurve();

        // animation curve for the green channel
        [KSPField] public FloatCurve greenCurve = new FloatCurve();

        // animation curve for the blue channel
        [KSPField] public FloatCurve blueCurve = new FloatCurve();

        // animation curve for the alpha channel
        [KSPField] public FloatCurve alphaCurve = new FloatCurve();

        // draper point, the temperature in Kelvin where materials start glowing
        [KSPField] public double draperPoint = 798.0;

        // temperature where the animation is at its maximum
        [KSPField] public double lerpMax = double.NaN;

        // temperature where the animation is at its minimum, added with draperPoint
        [KSPField] public double lerpMin = 0.0;

        // use the part's core temperature? (overrides useSkinTemp)
        [KSPField] public bool useCoreTemp = false;

        // use the part's skin temperature?
        [KSPField] public bool useSkinTemp = false;

        // should the module disable the stock blackbody glow effect on the included renderers?
        [KSPField] public bool disableBlackbody = false;

        public List<Renderer> renderers = new List<Renderer>();
        public List<string> excludedRendererNames = new List<string>();

        private readonly string _shaderBlackbody = "_TemperatureColor";

        private ModuleCoreHeat _coreHeatModule = null;

        private int _shaderPropertyID;

        private int _shaderBlackbodyID;

        private double _lerpRange;

        private Color _emissiveColor;
        private MaterialPropertyBlock _propertyBlock;

        public void Start()
        {
            if (base.vessel == null) return;

            _emissiveColor = new Color();
            _propertyBlock = new MaterialPropertyBlock();

            if (enableHeatEmissive)
            {
                if (useCoreTemp)
                {
                    _coreHeatModule = base.part.FindModuleImplementing<ModuleCoreHeat>();
                    if (_coreHeatModule == null)
                    {
                        this.LogError("Part has no Core Heat module, skipping");
                        useCoreTemp = false;
                    }
                }

                if (double.IsNaN(lerpMax))
                {
                    if (useCoreTemp)
                    {
                        lerpMax = _coreHeatModule.CoreShutdownTemp;
                    }
                    else
                    {
                        lerpMax = useSkinTemp ? part.skinMaxTemp : part.maxTemp;
                    }
                }

                _lerpRange = lerpMax - lerpMin - draperPoint;

                _shaderPropertyID = Shader.PropertyToID(shaderProperty);
            }

            if (disableBlackbody)
            {
                _shaderBlackbodyID = Shader.PropertyToID(_shaderBlackbody);
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight) return;

            if (node.HasValue("excludedRenderer"))
            {
                excludedRendererNames = new List<string>(node.GetValues("excludedRenderer"));
            }

            FindRenderers();
        }

        public void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            if (renderers == null) return;
            //when switching to the flight scene LateUpdate gets called AFTER OnLoad for some reason
            // so renderers should hopefully only be null for one frame

            if (enableHeatEmissive)
            {
                var temp = 0.0;
                if (useCoreTemp)
                {
                    temp = _coreHeatModule.CoreTemperature;
                }
                else
                {
                    temp = useSkinTemp ? base.part.skinTemperature : base.part.temperature;
                }

                var temp2 = (float)((temp - draperPoint) / _lerpRange);
                temp2 = Mathf.Clamp01(temp2);

                _emissiveColor.r = redCurve.Evaluate(temp2);
                _emissiveColor.g = greenCurve.Evaluate(temp2);
                _emissiveColor.b = blueCurve.Evaluate(temp2);
                _emissiveColor.a = alphaCurve.Evaluate(temp2);

                _propertyBlock.SetColor(_shaderPropertyID, _emissiveColor);
            }

            if (disableBlackbody)
            {
                _propertyBlock.SetColor(_shaderBlackbodyID, Color.black);
            }

            try
            {
                UpdateRenderers(_propertyBlock);
            } catch(NullReferenceException)
            {
                // if any renderers are null, rebuild renderer list
                // any bonus renderers will just have to be ignored I guess
                FindRenderers();
                UpdateRenderers(_propertyBlock);
            }
        }

        private void UpdateRenderers(MaterialPropertyBlock mpb)
        {
            for (var i = 0; i < renderers.Count; i++)
            {
                renderers[i].SetPropertyBlock(mpb);
            }
        }

        private void  FindRenderers()
        {
            renderers= part.FindModelComponents<Renderer>();

            renderers.RemoveAll(renderer => renderer == null);

            if( excludedRendererNames.Count != 0)
            {
                renderers.RemoveAll(renderer => excludedRendererNames.Contains(renderer.name));
            }
        }
    }
}