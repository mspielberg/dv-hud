using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Object = UnityEngine.Object;
using UnityEngine;
using System.Diagnostics;
using Cybex;
using Cybex.DERAILDigital;

namespace DvMod.HeadsUpDisplay
{
	public static class DERAILDigitalPusher
	{
		public static ITabletComputer? Instance;

		public static void Init()
		{
			IEnumerable<ITabletComputer>? t = Object.FindObjectsOfType<MonoBehaviour>().OfType<ITabletComputer>();
			if (t == null || t.Count() == 0)
			{
				TabletLoader.ControllerInstanceCreated += Init;
				UnityEngine.Debug.LogWarning("[HEADS UP DISPLAY] > [DERAIL Digital] No ITabletComputer Instance found!");
				return;
			}
			Instance = t.ElementAt(0);

			UnityEngine.Debug.LogWarning("[HEADS UP DISPLAY] > [DERAIL Digital] ITabletComputer Instance set. PushTrigger started!");
		}

		public static void Push()
		{
			if (Instance == null) return;

			
		}

		public static IEnumerator RunTest()
		{
			Stopwatch sw = new Stopwatch();

			List<Junction> junctions;
			RaycastHit[] hits = new RaycastHit[0];

			float footTot = 0, footAvg = -1, footMin = float.MaxValue, footMax = 0, footJ = 0;
			float scTot = 0, scAvg = -1, scMin = float.MaxValue, scMax = 0, scJ = 0;

			float t;
			int iterations = 2;// 2000;
			int sphereRange = 20000;

			while (true)
			{
				VisualSwitch[] switches = Object.FindObjectsOfType<VisualSwitch>();
				VisualSwitch s = switches.First();
				Junction j = s.junction;
				UnityEngine.Debug.Log($"\n----- Junction Detection Diagnostics -----\n" +
					$"\nJunction Info:\nHas Collider: {(j.GetComponent<Collider>() != null)}\nLayer: {LayerMask.LayerToName(j.gameObject.layer)} ({j.gameObject.layer})\n" +
					$"\nVisualSwitch Info:\nHas Collider: {(s.GetComponent<Collider>() != null)}\nLayer: {LayerMask.LayerToName(s.gameObject.layer)} ({s.gameObject.layer})");
				UnityEngine.Debug.Log($"\nStarting Test: ({iterations / 2} iterations each)");

				for (int i = 0; i < iterations; i++)
				{
					yield return null;

					if (i % 2 == 0)
					{
						sw.Restart();

						junctions = Object.FindObjectsOfType<Junction>().ToList();

						sw.Stop();
						t = sw.ElapsedMilliseconds;

						footJ = junctions.Count();
						footTot += t;
						if (t > footMax) footMax = t;
						if (t < footMin) footMin = t;
						if (footAvg == -1) footAvg = t;
						else footAvg = (footAvg + t) * 0.5f;
					}
					else
					{
						sw.Restart();

						hits = Physics.SphereCastAll(PlayerManager.LastLoco.transform.position, sphereRange, Vector3.up, 1, 1 << 15);

						junctions = hits
							.Where(x => x.collider.GetComponent<VisualSwitch>() != null)
							.Select(j => j.collider.GetComponent<VisualSwitch>().junction).ToList();

						sw.Stop();
						t = sw.ElapsedMilliseconds;

						scJ = junctions.Count();
						scTot += t;
						if (t > scMax) scMax = t;
						if (t < scMin) scMin = t;
						if (scAvg == -1) scAvg = t;
						else scAvg = (scAvg + t) * 0.5f;
					}
				}

				UnityEngine.Debug.Log($"\nDiagnostics Result:\n");

				UnityEngine.Debug.Log($"FindObjectsOfType:\n> Detected Junctions: {footJ}\n> t avg: {footAvg} ms\n> t min: {footMin} ms\n> t max: {footMax} ms\n> t tot: {footTot} ms");
				UnityEngine.Debug.Log($"\nSphereCastAll ({sphereRange} units range):\n> Detected Colliders: {hits.Count()}\n> Detected Junctions: {scJ}\n> t avg: {scAvg} ms\n> t min: {scMin} ms\n> t max: {scMax} ms\n> t tot: {scTot} ms");

				UnityEngine.Debug.Log($"\n------------------------------------------\n");

				yield break;
			}
		}
	}
}
