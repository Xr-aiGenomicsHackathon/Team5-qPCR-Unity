using System;
using System.Collections.Generic;
using UnityEngine;

namespace Team5.qPCR
{
    public enum QpcrProtocolParameter
    {
        ReactionVolume,
        LidTemperature,
        InitialDenaturation,
        AmplificationCycles,
        Denaturation,
        Annealing,
        Extension,
        FluorescenceAcquisition,
        MeltCurveStart,
        MeltCurveEnd,
        TemperatureIncrement,
        PostRunHold,
        InitialHoldSeconds,
        DenaturationSeconds,
        AnnealingSeconds,
        ExtensionSeconds
    }

    [Serializable]
    public sealed class QpcrProtocolFieldView
    {
        public QpcrProtocolParameter Parameter;
        public TMPro.TMP_Text Label;
        public TMPro.TMP_Text Value;
        public UnityEngine.UI.Image Background;
        public UnityEngine.UI.Button SelectButton;
    }

    public sealed class QpcrProtocolValidator : MonoBehaviour
    {
        [SerializeField, Min(0.0001f)] private float tolerance = 0.01f;

        public bool IsCorrect(QpcrProtocolParameter parameter, float value) =>
            Mathf.Abs(value - ExpectedValue(parameter)) <= tolerance;

        public bool ValidateAll(IReadOnlyDictionary<QpcrProtocolParameter, float> values,
            out QpcrProtocolParameter firstInvalid)
        {
            foreach (QpcrProtocolParameter parameter in Enum.GetValues(typeof(QpcrProtocolParameter)))
            {
                if (!values.TryGetValue(parameter, out var value) || !IsCorrect(parameter, value))
                {
                    firstInvalid = parameter;
                    return false;
                }
            }
            firstInvalid = default;
            return true;
        }

        public static float ExpectedValue(QpcrProtocolParameter parameter) => parameter switch
        {
            QpcrProtocolParameter.ReactionVolume => 20f,
            QpcrProtocolParameter.LidTemperature => 105f,
            QpcrProtocolParameter.InitialDenaturation => 95f,
            QpcrProtocolParameter.AmplificationCycles => 35f,
            QpcrProtocolParameter.Denaturation => 95f,
            QpcrProtocolParameter.Annealing => 60f,
            QpcrProtocolParameter.Extension => 72f,
            QpcrProtocolParameter.FluorescenceAcquisition => 60f,
            QpcrProtocolParameter.MeltCurveStart => 65f,
            QpcrProtocolParameter.MeltCurveEnd => 95f,
            QpcrProtocolParameter.TemperatureIncrement => 0.5f,
            QpcrProtocolParameter.PostRunHold => 4f,
            QpcrProtocolParameter.InitialHoldSeconds => 120f,
            QpcrProtocolParameter.DenaturationSeconds => 15f,
            QpcrProtocolParameter.AnnealingSeconds => 30f,
            QpcrProtocolParameter.ExtensionSeconds => 30f,
            _ => 0f
        };

        public static string LabelFor(QpcrProtocolParameter parameter) => parameter switch
        {
            QpcrProtocolParameter.ReactionVolume => "Reaction volume",
            QpcrProtocolParameter.LidTemperature => "Lid temperature",
            QpcrProtocolParameter.InitialDenaturation => "Initial denaturation",
            QpcrProtocolParameter.AmplificationCycles => "Amplification cycles",
            QpcrProtocolParameter.Denaturation => "Denaturation",
            QpcrProtocolParameter.Annealing => "Annealing",
            QpcrProtocolParameter.Extension => "Extension",
            QpcrProtocolParameter.FluorescenceAcquisition => "Fluorescence acquisition",
            QpcrProtocolParameter.MeltCurveStart => "Melt curve start",
            QpcrProtocolParameter.MeltCurveEnd => "Melt curve end",
            QpcrProtocolParameter.TemperatureIncrement => "Temperature increment",
            QpcrProtocolParameter.PostRunHold => "Post-run hold",
            QpcrProtocolParameter.InitialHoldSeconds => "Initial hold time",
            QpcrProtocolParameter.DenaturationSeconds => "Denaturation time",
            QpcrProtocolParameter.AnnealingSeconds => "Annealing time",
            QpcrProtocolParameter.ExtensionSeconds => "Extension time",
            _ => parameter.ToString()
        };

        public static string UnitFor(QpcrProtocolParameter parameter) => parameter switch
        {
            QpcrProtocolParameter.ReactionVolume => "µL",
            QpcrProtocolParameter.AmplificationCycles => "cycles",
            QpcrProtocolParameter.TemperatureIncrement => "°C",
            QpcrProtocolParameter.InitialHoldSeconds or QpcrProtocolParameter.DenaturationSeconds or
                QpcrProtocolParameter.AnnealingSeconds or QpcrProtocolParameter.ExtensionSeconds => "s",
            _ => "°C"
        };
    }
}
