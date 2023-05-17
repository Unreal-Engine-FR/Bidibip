const path = require('path');
require('dotenv').config({path: path.join(__dirname, '../.env')});
const handlers = require('./handlers');

const AutoGitUpdate = require('auto-git-update');

const config = {
    repository: 'https://github.com/Unreal-Engine-FR/Bidibip',
    branch: 'bidibip-v3',
    tempLocation: '../tmp/',
    ignoreFiles: ['.env'],
    //executeOnComplete: 'C:/Users/scheg/Desktop/worksapce/AutoGitUpdate/startTest.bat',
    exitOnComplete: true
}

const updater = new AutoGitUpdate(config);

updater.autoUpdate();

const Discord = require('discord.js');
const client = new Discord.Client(
    {
        partials: [Discord.Partials.Channel],
        intents: [
            Discord.GatewayIntentBits.Guilds,
            Discord.GatewayIntentBits.GuildMessages,
            Discord.GatewayIntentBits.GuildMembers,
            Discord.GatewayIntentBits.MessageContent,
            Discord.GatewayIntentBits.DirectMessages
        ]
    }
);

// Overwrite console.log to keep log file
if (process.env.ENV === 'PROD') {
    let fs = require('fs');
    let log_file = fs.createWriteStream('./debug.log', {flags: 'a'});
    let log_stdout = process.stdout;
    console.log = (text) => {
        const now = new Date().toISOString();
        const date = now.substr(2, 8) + ' ' + now.substr(11, 8);
        const string = `[${date}] ${text}\n`;
        log_file.write(string);
        log_stdout.write(string);

    };

    //Delete each month 2628000000
    setInterval(() => fs.truncate('./debug.log', 0, () => console.log('[LOGS DELETED]')), 1209600000);
}

client.inProcessAdvert = {};

// All DiscordJs events here: https://discord.js.org/#/docs/main/stable/class/Client?scrollTo=e-channelCreate
client.on('ready', () => handlers.ready(client));
client.on('guildMemberAdd', member => handlers.guildMemberAdd(client));
client.on('guildMemberRemove', member => handlers.guildMemberRemove(client));
client.on('messageDelete', msg => handlers.messageDelete(client, msg));
client.on('messageUpdate', (oldMsg, newMsg) => handlers.messageUpdate(client, oldMsg, newMsg));
client.on('messageCreate', msg => handlers.message(client, msg));
client.on('voiceStateUpdate', (oldState, newState) => handlers.voiceStateUpdate(client, oldState, newState));

/*
client.on('ready', () => {console.log("ready")})
client.on('guildMemberAdd', member => {console.log("guild member add")})
client.on('guildMemberRemove', member => {console.log("guild member remove")})
client.on('messageDelete', msg => {console.log("message delete")})
client.on('messageUpdate', (oldMsg, newMsg) => {console.log("message update")})
client.on('messageCreate', msg => {console.log("message")})
client.on('voiceStateUpdate', (oldState, newState) => {console.log("voice state update")})
*/


client.login(process.env.TOKEN);
