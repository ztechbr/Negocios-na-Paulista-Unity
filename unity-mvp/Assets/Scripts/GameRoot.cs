using UnityEngine;

namespace PaulistaBusinessWorld
{
    // Ponto de entrada: monta mundo, jogador e HUD. Funciona em qualquer cena.
    public class GameRoot : MonoBehaviour
    {
        void Awake()
        {
            Application.targetFrameRate = 60;

            var world = new GameObject("Mundo").AddComponent<WorldBuilder>();
            world.transform.SetParent(transform);
            world.Build();

            LocalModel.Init(); // banco de negócios e modelo local (SQLite no Windows)

            var saved = PlayerProfile.Load();
            var profile = saved ?? new PlayerProfile();

            var playerGo = new GameObject("Jogador");
            playerGo.transform.SetParent(transform);
            playerGo.AddComponent<CharacterController>();
            var player = playerGo.AddComponent<PlayerController>();
            player.Init(profile, world);

            var hud = gameObject.AddComponent<GameHUD>();
            hud.Init(world, player, profile, showAvatarEditor: saved == null);

            // Uso: -pbw-screenshot <arquivo.png> [-pbw-panorama] [-pbw-chat <n>] [-pbw-editor]
            // Captura a tela após o carregamento e fecha.
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-pbw-screenshot");
            if (i >= 0 && i + 1 < args.Length)
            {
                if (System.Array.IndexOf(args, "-pbw-editor") < 0) hud.CloseAvatarEditor();
                if (System.Array.IndexOf(args, "-pbw-panorama") >= 0) player.TogglePanorama();
                int c = System.Array.IndexOf(args, "-pbw-chat");
                if (c >= 0 && c + 1 < args.Length && int.TryParse(args[c + 1], out int npc))
                {
                    var p = WorldData.People[npc].Position;
                    player.Teleport(p + new Vector3(-2.5f, 0.3f, p.z < 0 ? 1.5f : -1.5f), 90);
                    hud.StartConversation(world.Npcs[npc]);
                    for (int k = 0; k + 1 < args.Length; k++)
                        if (args[k] == "-pbw-say") hud.SayInConversation(args[k + 1]);
                    int neg = System.Array.IndexOf(args, "-pbw-offer");
                    if (neg >= 0 && neg + 1 < args.Length) hud.TestNegotiation(float.Parse(args[neg + 1], System.Globalization.CultureInfo.InvariantCulture));
                }
                StartCoroutine(CaptureAndQuit(args[i + 1]));
            }
        }

        System.Collections.IEnumerator CaptureAndQuit(string path)
        {
            yield return new WaitForSeconds(3);
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSeconds(1);
            Application.Quit();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureGameRoot()
        {
            if (FindAnyObjectByType<GameRoot>() != null) return;
            foreach (var cam in FindObjectsByType<Camera>())
                cam.gameObject.SetActive(false);
            new GameObject("Paulista Business World").AddComponent<GameRoot>();
        }
    }
}
