module.exports = {
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
};