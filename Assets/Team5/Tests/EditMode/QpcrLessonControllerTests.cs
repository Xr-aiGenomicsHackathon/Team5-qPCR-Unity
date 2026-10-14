using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Team5.qPCR.Tests
{
    public sealed class QpcrLessonControllerTests
    {
        private GameObject owner;
        private QpcrLessonController lesson;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("qPCR lesson test");
            lesson = owner.AddComponent<QpcrLessonController>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(owner);

        [Test]
        public void LessonUsesTheRequiredVrFirstOrder()
        {
            lesson.BeginLesson();
            Assert.AreEqual(QpcrLessonState.InspectPlateId, lesson.CurrentState);
            Assert.AreEqual("Plate ID label", lesson.CurrentTarget);

            var ordered = new[]
            {
                QpcrLessonState.InspectPlateId, QpcrLessonState.InspectA1Marker,
                QpcrLessonState.InspectOpticalSeal, QpcrLessonState.InspectBubbles,
                QpcrLessonState.ReturnPlate, QpcrLessonState.PowerOn,
                QpcrLessonState.OpenDrawer, QpcrLessonState.LoadPlate,
                QpcrLessonState.CloseDrawer, QpcrLessonState.ConfigureProtocol,
                QpcrLessonState.ValidateProtocol, QpcrLessonState.StartRun,
                QpcrLessonState.ObserveAmplification, QpcrLessonState.InterpretResults
            };
            foreach (var state in ordered)
                Assert.IsTrue(lesson.TryComplete(state), state.ToString());

            Assert.AreEqual(QpcrLessonState.Complete, lesson.CurrentState);
            Assert.IsFalse(lesson.IsLessonActive);
        }

        [Test]
        public void WrongActionDoesNotSkipTheCurrentScientificGate()
        {
            string feedback = null;
            lesson.FeedbackRequested += (message, _) => feedback = message;
            lesson.BeginLesson();

            Assert.IsFalse(lesson.TryComplete(QpcrLessonState.PowerOn));
            Assert.AreEqual(QpcrLessonState.InspectPlateId, lesson.CurrentState);
            Assert.That(feedback, Does.Contain("Plate ID label"));
        }

        [Test]
        public void PlateInspectionExplainsObservationAndConfirmation()
        {
            var presentation = QpcrLessonController.GetPresentation(QpcrLessonState.InspectPlateId);

            Assert.That(presentation.Instruction, Does.Contain("Confirm Plate ID"));
            Assert.That(QpcrLessonController.GetInputHint(QpcrLessonState.InspectPlateId), Does.Contain("Trigger"));
            Assert.AreEqual(1, QpcrLessonController.GetStepNumber(QpcrLessonState.InspectPlateId));
            Assert.AreEqual(15, QpcrLessonController.GetStepNumber(QpcrLessonState.Complete));
        }

        [Test]
        public void TrainingProtocolContainsAllTwelveApprovedValues()
        {
            var validator = owner.AddComponent<QpcrProtocolValidator>();
            var values = new Dictionary<QpcrProtocolParameter, float>();
            foreach (QpcrProtocolParameter parameter in System.Enum.GetValues(typeof(QpcrProtocolParameter)))
                values[parameter] = QpcrProtocolValidator.ExpectedValue(parameter);

            Assert.IsTrue(validator.ValidateAll(values, out _));
            Assert.AreEqual(20f, values[QpcrProtocolParameter.ReactionVolume]);
            Assert.AreEqual(105f, values[QpcrProtocolParameter.LidTemperature]);
            Assert.AreEqual(35f, values[QpcrProtocolParameter.AmplificationCycles]);
            Assert.AreEqual(.5f, values[QpcrProtocolParameter.TemperatureIncrement]);
            Assert.AreEqual(4f, values[QpcrProtocolParameter.PostRunHold]);
        }

        [Test]
        public void OneIncorrectProtocolValueBlocksValidation()
        {
            var validator = owner.AddComponent<QpcrProtocolValidator>();
            var values = new Dictionary<QpcrProtocolParameter, float>();
            foreach (QpcrProtocolParameter parameter in System.Enum.GetValues(typeof(QpcrProtocolParameter)))
                values[parameter] = QpcrProtocolValidator.ExpectedValue(parameter);
            values[QpcrProtocolParameter.Annealing] = 72f;

            Assert.IsFalse(validator.ValidateAll(values, out var invalid));
            Assert.AreEqual(QpcrProtocolParameter.Annealing, invalid);
        }
    }
}
