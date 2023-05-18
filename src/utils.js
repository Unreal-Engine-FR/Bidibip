/* eslint-disable max-len */
let fs = require('fs');
const quotes = require('./modules/quote/data/quotes.json');
const Discord = require('discord.js');

module.exports = {
    writeFile         : (absolutePath, str) => fs.writeFileSync(absolutePath, str),
    tryToSend         : (channel, text) => {
        console.log(text)
        if (text.embed) {
            const newEmbed = new Discord.EmbedBuilder()
                .setTitle(text.embed.title)
                .setDescription(text.embed.description)
                .setColor(Number(text.embed.color))
            if (text.embed.author)
                newEmbed.setAuthor({ name: text.embed.author.name, iconURL: text.embed.author.iconURL, url: 'https://discord.js.org' })
            channel.send({embeds: [newEmbed]});
        }
        else if (text.content)
            channel.send(text);
        else
            channel.send({content: text});
    },
    tryToSendChannelId: (client, channelId, text) => {
        const channel = client.channels.cache.get(channelId);
        if (!channel) throw new Error('Channel ID not found: ' + channelId);

        module.exports.tryToSend(channel, text);
    },
    getMemberCount: async (client, serverId = process.env.SERVER_ID) => {
        const guild = await client.guilds.fetch(serverId);
        const members = await guild.members.fetch();
        const memberCount = members.filter(member => member.user.bot === false).size;
        return memberCount;
    },
    updateClientActivity: (client) => {
        const serverId = process.env.SERVER_ID;
        return module.exports.getMemberCount(client, serverId).then(memberCount => {
            client.user.setActivity(memberCount + ' utilisateurs', {type: 'WATCHING'});
        });
    },
    addQuote: (username, msgContent) => {
        // clean user name, so only first word is used.
        // If the name has a space in it, it would not recognize it
        // as only the first word of command is understood by the bot
        username = username.split(' ')[0];
        if (quotes[username]) quotes[username].push(msgContent);
        else quotes[username] = [msgContent];

        module.exports.writeFile('data/quotes.json', JSON.stringify(quotes));

        console.log(`[QUOTE ADDED] A quote from ${username} has been saved`);
    },

    advertToEmbedUnpaid: (advert, user) => {
        console.log(process.env.COLOR_PAID)
        const embed = new Discord.EmbedBuilder();

        embed.setColor(Number(process.env.COLOR_UNPAID));

        const mapping = {
            title      : (text) => embed.setTitle(text),
            description: (text) => embed.setDescription(text),
            contact    : (text) => embed.addFields([{name: '**Contact**', value: text}]),
        };

        for (const key in advert) {
            if (['finish'].includes(key)) continue;

            if (key in mapping) mapping[key](advert[key]);
            else console.log(`'${key}' mapping not found in advertToEmbedUnpaid()`);
        }

        return {
            content: `Publié par : <@${user.id}>`,
            embeds: [embed]
        };
    },

    advertToEmbedPaid: (advert, user) => {
        const embed = new Discord.EmbedBuilder()
            .setColor(process.env.COLOR_PAID)

        const mapping = {
            role  : () => embed.setTitle(`${advert.role} Chez ${advert.companyName}`),
            remote: (text) => {
                if (text == 1) embed.setDescription(':globe_with_meridians: Remote accepté');
            },
            localisation: (text) => embed.addFields([{name:'**Localisation**', value: text, inline: true}]),
            contract    : (text) => {
                if (text == 2) embed.addFields([{name:'**Durée du contrat**', value: advert.length, inline:true}]);
            },
            responsabilities: (text) => embed.addFields([{name: '**Responsabilités**\n', value: text}]),
            qualifications  : (text) => embed.addFields([{name:'**Qualifications**\n', value:text}]),
            apply           : (text) => embed.addFields([{name:'**Comment postuler**\n', value:text}]),
            pay             : () => { },
            companyName     : () => { },
        };

        for (const key in advert) {
            if (['finish'].includes(key)) continue;

            if (key in mapping) mapping[key](advert[key]);
            else console.log(`'${key}' mapping not found in advertToEmbedPaid()`);
        }

        return {
            content: `Publié par : <@${user.id}>`,
            embeds: [embed]
        };
    },
    advertToEmbedFreelance: (advert, user) => {
        const embed = new Discord.EmbedBuilder();

        embed.setColor(process.env.COLOR_FREELANCE);

        const mapping = {
            title      : (text) => embed.setTitle(text),
            url        : (text) => embed.setDescription(text),
            description: (text) => embed.addFields([{name:'**Services**\n', value:text}]),
            contact    : (text) => embed.addFields([{name:'**Contact**\n', value:text}]),
        };

        for (const key in advert) {
            if (['finish'].includes(key)) continue;

            if (key in mapping) mapping[key](advert[key]);
            else console.log(`'${key}' mapping not found in advertToEmbedFreelance()`);
        }

        return {
            content: `Publié par : <@${user.id}>`,
            embeds: [embed]
        };
    }
};