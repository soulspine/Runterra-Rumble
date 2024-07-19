using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using System.Windows;

namespace Runterra_Rumble
{
    public static class CustomAnimation
    {
        public static StringAnimationUsingKeyFrames DeleteAndTyping(string from, string to, double duration)
        {
            var keyFrames = new StringKeyFrameCollection();

            int deleteActions = from.Length + 1;
            int addActions = to.Length;
            int totalActions = deleteActions + addActions;

            for (int i = 0; i < deleteActions; i++)
            {
                keyFrames.Add(new DiscreteStringKeyFrame(from.Substring(0, deleteActions - i - 1), KeyTime.FromTimeSpan(TimeSpan.FromSeconds((duration / totalActions) * i))));
            }

            for (int i = 1; i < addActions + 1; i++)
            {
                keyFrames.Add(new DiscreteStringKeyFrame(to.Substring(0, i), KeyTime.FromTimeSpan(TimeSpan.FromSeconds((duration / totalActions) * (deleteActions + i)))));
            }

            return new StringAnimationUsingKeyFrames()
            {
                Duration = new Duration(TimeSpan.FromSeconds(duration)),
                KeyFrames = keyFrames,
            };
        }
    }
}