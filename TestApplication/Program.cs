
using ArxOne.Synology;

using var spkStream = File.OpenRead(@"/arx/.data/software/backup/dsm/ArxOneBackup-10.0.17913.1222[noarch-7.0-40000].spk");
var spkReader = new SpkReader(spkStream);
spkReader.Read();