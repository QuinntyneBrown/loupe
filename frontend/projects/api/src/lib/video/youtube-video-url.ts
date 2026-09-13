const HOSTS = [
  'youtube.com',
  'www.youtube.com',
  'm.youtube.com',
  'music.youtube.com',
  'youtube-nocookie.com',
  'www.youtube-nocookie.com',
];
const SHORT_HOSTS = ['youtu.be', 'www.youtu.be'];
const PATH_PREFIXES = ['/shorts/', '/live/', '/embed/', '/v/'];
const ID = /^[A-Za-z0-9_-]{11}$/;

/** Returns the 11-character video identifier for a YouTube video URL, or null when the URL is not one. */
export function parseYouTubeVideoId(value: string): string | null {
  let url: URL;
  try {
    url = new URL(value.trim());
  } catch {
    return null;
  }
  if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.port)
    return null;
  const host = url.hostname.toLowerCase();
  let candidate: string | null = null;
  if (SHORT_HOSTS.includes(host)) candidate = url.pathname.replace(/^\/+|\/+$/g, '');
  else if (HOSTS.includes(host)) {
    if (url.pathname === '/watch') candidate = url.searchParams.get('v');
    else {
      const prefix = PATH_PREFIXES.find((prefix) => url.pathname.startsWith(prefix));
      if (prefix) candidate = url.pathname.slice(prefix.length).replace(/\/+$/g, '');
    }
  }
  return candidate && ID.test(candidate) ? candidate : null;
}
