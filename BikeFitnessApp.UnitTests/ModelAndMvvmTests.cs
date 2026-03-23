using Microsoft.VisualStudio.TestTools.UnitTesting;
using BikeFitnessApp.Models;
using BikeFitnessApp.MVVM;
using System.ComponentModel;

namespace BikeFitnessApp.UnitTests
{
    [TestClass]
    public class ModelAndMvvmTests
    {
        [TestMethod]
        public void StravaToken_Properties_WorkCorrectly()
        {
            // Arrange
            var token = new StravaToken();
            string accessToken = "abc-123";
            string refreshToken = "xyz-789";
            long expiresAt = 1234567890;

            // Act
            token.AccessToken = accessToken;
            token.RefreshToken = refreshToken;
            token.ExpiresAt = expiresAt;

            // Assert
            Assert.AreEqual(accessToken, token.AccessToken);
            Assert.AreEqual(refreshToken, token.RefreshToken);
            Assert.AreEqual(expiresAt, token.ExpiresAt);
        }

        private class TestObservableObject : ObservableObject
        {
            private string _name = string.Empty;
            public string Name
            {
                get => _name;
                set => SetProperty(ref _name, value);
            }

            public bool SetName(string name)
            {
                return SetProperty(ref _name, name, nameof(Name));
            }

            public void RaiseManualChange(string propName)
            {
                OnPropertyChanged(propName);
            }
        }

        [TestMethod]
        public void ObservableObject_SetProperty_UpdatesValueAndRaisesEvent()
        {
            // Arrange
            var obj = new TestObservableObject();
            string eventPropertyName = string.Empty;
            obj.PropertyChanged += (s, e) => eventPropertyName = e.PropertyName ?? string.Empty;

            // Act
            bool result = obj.SetName("New Name");

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual("New Name", obj.Name);
            Assert.AreEqual("Name", eventPropertyName);
        }

        [TestMethod]
        public void ObservableObject_SetProperty_DoesNotRaiseEventIfSameValue()
        {
            // Arrange
            var obj = new TestObservableObject();
            obj.SetName("Same Value");
            bool eventRaised = false;
            obj.PropertyChanged += (s, e) => eventRaised = true;

            // Act
            bool result = obj.SetName("Same Value");

            // Assert
            Assert.IsFalse(result);
            Assert.IsFalse(eventRaised);
        }

        [TestMethod]
        public void ObservableObject_OnPropertyChanged_RaisesEvent()
        {
            // Arrange
            var obj = new TestObservableObject();
            string eventPropertyName = string.Empty;
            obj.PropertyChanged += (s, e) => eventPropertyName = e.PropertyName ?? string.Empty;

            // Act
            obj.RaiseManualChange("ManualProp");

            // Assert
            Assert.AreEqual("ManualProp", eventPropertyName);
        }
    }
}
