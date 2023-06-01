// MODULE REPOST
const {CommandInfo} = require("../../utils/interactionBase")
const {Message} = require("../../utils/message")
const {Channel} = require("../../utils/channel")
const {Embed} = require("../../utils/embed")
const {Button} = require("../../utils/button")
const {InteractionRow} = require("../../utils/interaction_row")
const {Thread} = require("../../utils/thread")
const {ModuleBase} = require("../../utils/module_base")

function make_key(message) {
    return `${message.channel().id()}/${message.id()}`
}

/**
 * @param key {string}
 * @returns {Message}
 */
function message_from_key(key) {
    const split = key.split('/')
    return new Message().set_id(split[1]).set_channel(new Channel().set_id(split[0]))
}

async function format_message(message, header) {
    const message_group = []
    const author = await message.author()
    const channel = message.channel()
    message_group.push(
        new Message()
            .set_text(`${header}${channel.url()} !`)
            .add_embed(
                new Embed()
                    .set_author(author)
                    .set_description(await message.text())))

    for (const hyperlink of find_urls(await message.text()))
        message_group.push(new Message()
            .set_text(hyperlink))

    for (const attachment of message.attachments())
        message_group.push(new Message()
            .set_text(attachment.file()))

    // Add url to last message
    message_group[message_group.length - 1]
        .add_interaction_row(
            new InteractionRow()
                .add_button(new Button()
                    .set_label('Viens donc voir !')
                    .set_type(Button.Link)
                    .set_url(message.url())))

    return message_group
}

function find_urls(initial_text) {
    const split = initial_text.split(/\r\n|\r|\n| /)
    const attachments = []
    for (let i = split.length - 1; i >= 0; --i)
        if (split[i].includes('http'))
            attachments.push(split[i])

    return attachments
}

class Module extends ModuleBase {
    constructor(create_infos) {
        super(create_infos);

        // These are the channels waiting for the first message
        this.threads_waiting_first_message = new Set()

        this.commands = [
            new CommandInfo('set-forum-link', 'Lie un forum a un channel de repost', this.set_forum_link)
                .add_channel_option('forum', 'Forum où seront suivis les nouveaux posts')
                .add_channel_option('repost-channel', 'Canal où seront repostés les évenements du forum')
                .add_bool_option('vote', 'Avec ou sans fonctionnalités de vote')
                .add_bool_option('enabled', 'Active ou desactive le lien', false, true)
                .set_admin_only(),
            new CommandInfo('view-forum-links', 'Voir la liste des liens entre forums et salons', this.view_forum_link)
                .set_admin_only(),
            new CommandInfo('promote', 'Promeut le message donné dans le salon de repost', this.promote)
                .add_message_option('message', 'lien du message à promouvoir'),
        ]

        this.load_config({
            reposted_forums: {},
            repost_votes: {},
            vote_messages: {}
        })

        this.bound_message = {}
        // Keep track for vote button
        for (const [key, value] of Object.entries(this.module_config.vote_messages)) {
            this.bind_vote_button(message_from_key(key), new Thread().set_id(value))
                .catch(err => console.error(`Failed to update vote button : ${err}`))
        }
    }

    /**
     * @param command {CommandInteraction}
     * @return {Promise<void>}
     */
    async set_forum_link(command) {

        let source_forum = null
        try {
            source_forum = new Channel().set_id(command.read('forum'))
            if ((await source_forum.type()) !== Channel.TypeForum) {
                throw new Error('Channel is not a forum')
            }
        } catch (err) {
            console.warning(`'forum' option does not correspond to a forum : ${err}`)
            await command.reply(new Message().set_text(`Le salon que vous avez fourni n'est pas un forum`).set_client_only())
            return
        }
        const bound_channels = new Channel().set_id(command.read('repost-channel'))
        const enable = command.read('enabled')

        if ((await bound_channels.type()) !== Channel.TypeChannel) {
            console.warning(`'repost-channel' option does not correspond to a standard channel`)
            await command.reply(new Message().set_text(`Le salon de repost que vous avez fourni n'est pas un salon standard`).set_client_only())
            return
        }

        if (enable) {
            console.info('created link between', source_forum, 'and', bound_channels)

            if (this.module_config.reposted_forums[source_forum.id()]) {
                if (this.module_config.reposted_forums[source_forum.id()].bound_channels.indexOf(bound_channels.id()) === -1)
                    this.module_config.reposted_forums[source_forum.id()].bound_channels.push(bound_channels.id())
            } else this.module_config.reposted_forums[source_forum.id()] = {
                bound_channels: [bound_channels.id()]
            }

            this.module_config.reposted_forums[source_forum.id()].vote = command.read('vote')

            this.save_config()
            await command.reply(new Message().set_text(`Lien créé`).set_client_only())
        } else {
            if (this.module_config.reposted_forums[source_forum.id()]) {
                if (this.module_config.reposted_forums[source_forum.id()].bound_channels.indexOf(bound_channels.id()) !== -1) {
                    this.module_config.reposted_forums[source_forum.id()].bound_channels.splice(this.module_config.reposted_forums[source_forum.id()].bound_channels.indexOf(bound_channels.id()), 1)
                    await command.reply(new Message().set_text(`Lien supprimé`).set_client_only())
                }
                if (this.module_config.reposted_forums[source_forum.id()].bound_channels.length === 0)
                    delete this.module_config.reposted_forums[source_forum.id()]
                this.save_config()
            } else
                await command.reply(new Message().set_text(`Ce lien n'existe pas`).set_client_only())
        }
    }

    /**
     * @param command {CommandInteraction}
     * @return {Promise<void>}
     */
    async view_forum_link(command) {
        const embed = new Embed()
            .set_title('Liens de repost')
            .set_description('Liste des liens de repost entre forums et salons de repost')
        for (const [key, value] of Object.entries(this.module_config.reposted_forums)) {
            let forums = ''
            for (const forum of value.bound_channels) {
                forums += `Vers ${new Channel().set_id(forum).url()}\n`
            }
            embed.add_field(`De ${new Channel().set_id(key).url()} (vote : ${value.vote ? ':white_check_mark:' : ':x:'})`, forums.length === 0 ? 'Pas de lien' : forums)
        }

        await command.reply(
            new Message()
                .add_embed(embed)
                .set_client_only())
    }

    /**
     * @param command {CommandInteraction}
     * @return {Promise<void>}
     */
    async promote(command) {
        // The message we should promote
        const message_to_promote = command.read('message')

        // Ensure message exists
        if (!await message_to_promote.exists())
            return command.reply(new Message().set_client_only().set_text('Ce message n\'existe pas'))

        // Ensure message was posted inside a forum
        const thread = new Thread().set_id(message_to_promote.channel().id())
        const forum = await thread.parent_channel()
        if (!forum || await forum.type() !== Channel.TypeForum)
            return command.reply(new Message().set_client_only().set_text('Le message doit provenir d\'un forum'))

        // Ensure command author is allowed to promote this message
        if ((await thread.owner()).id() !== command.author().id())
            return command.reply(new Message().set_client_only().set_text('Vous devez être l\'auteur du salon d\'où le message sera promu'))

        // Ensure repost is enabled
        if (!this.module_config.reposted_forums[forum.id()])
            return command.reply(new Message().set_client_only().set_text(`Le repost n'est pas activé dans ce forum`))

        const message_to_send = await format_message(message_to_promote, 'Mise à jour dans ')
            .catch(err => console.fatal(`Failed to create update messages : ${err}`))

        // Repost in every channels
        for (const channel of this.module_config.reposted_forums[forum.id()].bound_channels)
            for (const repost_message of message_to_send)
                await repost_message.set_channel(new Channel().set_id(channel)).send()

        await command.reply(new Message().set_client_only().set_text('Ton post a été promu !'))
    }

    /**
     * @param thread {Thread}
     * @returns {Promise<void>}
     */
    async thread_created(thread) {
        // We detected a thread have been created. Now we wait it's first message
        if (this.module_config.reposted_forums[(await thread.parent_channel()).id()])
            this.threads_waiting_first_message.add(thread.id())
    }

    /**
     * On received server messages
     * @param message {Message}
     * @return {Promise<void>}
     */
    async server_message(message) {
        const thread = await message.channel()

        // Ensure the thread that contains the message is waiting for it's first message
        if (!this.threads_waiting_first_message.has(thread.id()))
            return

        // Don't track anymore
        this.threads_waiting_first_message.delete(thread.id())

        const forum = await thread.parent_channel()

        // Repost is not enabled for this forum
        if (!this.module_config.reposted_forums[forum.id()])
            return

        // Create message
        const messages = (await format_message(message, `Nouveau post dans ${await forum.name()} : `))
        const last_message = messages.pop()

        for (const channel of this.module_config.reposted_forums[forum.id()].bound_channels) {
            for (const repost_message of messages)
                await repost_message
                    .set_channel(new Channel().set_id(channel))
                    .send()

            await last_message
                .set_channel(new Channel().set_id(channel))
                .send()
                .then(reposted_message => this.bind_vote_button(reposted_message, thread))
        }
    }

    async create_or_update_vote_buttons(message, vote_thread) {
        let yes_button = await message.get_button_by_id('button-vote-yes')
        let no_button = await message.get_button_by_id('button-vote-no')
        if (message._interactions.length === 0) message.add_interaction_row(new InteractionRow())
        const interaction_row = message._interactions[0]
        if (!yes_button) {
            yes_button = new Button()
                .set_id('button-vote-yes')
                .set_type(Button.Primary)
            interaction_row.add_button(yes_button)
        }
        if (!no_button) {
            no_button = new Button()
                .set_id('button-vote-no')
                .set_type(Button.Primary)
            interaction_row.add_button(no_button)
        }
        yes_button.set_label(`Pour ✅ ${Object.entries(this.module_config.repost_votes[vote_thread.id()].vote_yes).length}`)
        no_button.set_label(`Contre ❌ ${Object.entries(this.module_config.repost_votes[vote_thread.id()].vote_no).length}`)

        // Update button
        await message.update(message)

        if (!this.module_config.vote_messages[make_key(message)]) {
            this.module_config.vote_messages[make_key(message)] = vote_thread.id()
            this.save_config()
        }
    }

    async bind_vote_button(message, thread) {
        // Not a thread
        if (!await thread.parent_channel())
            return

        // Check if vote is enabled or not
        if (!this.module_config.reposted_forums[(await thread.parent_channel()).id()].vote)
            return

        // Init if not exists
        if (!this.module_config.repost_votes[thread.id()]) {
            this.module_config.repost_votes[thread.id()] = {
                vote_yes: {},
                vote_no: {},
                bound_messages: []
            }
            this.save_config()
        }

        if (this.module_config.repost_votes[thread.id()].bound_messages.indexOf(make_key(message)) === -1)
            this.module_config.repost_votes[thread.id()].bound_messages.push(make_key(message))

        this.bind_button(message, async (button_interaction) => {
            const vote_infos = this.module_config.repost_votes[thread.id()]
            if (button_interaction.button_id() === 'button-vote-yes') {
                vote_infos.vote_yes[button_interaction.author().id()] = true
                if (vote_infos.vote_no[button_interaction.author().id()])
                    delete vote_infos.vote_no[button_interaction.author().id()]
                this.save_config()
            } else if (button_interaction.button_id() === 'button-vote-no') {
                vote_infos.vote_no[button_interaction.author().id()] = true
                if (vote_infos.vote_yes[button_interaction.author().id()])
                    delete vote_infos.vote_yes[button_interaction.author().id()]
                this.save_config()
            }

            for (const message_to_update of vote_infos.bound_messages)
                await this.create_or_update_vote_buttons(message_from_key(message_to_update), thread)
                    .catch(err => console.error(`Failed to update vote button : ${err}`))
            await button_interaction.skip()
        })

        await this.create_or_update_vote_buttons(message, thread)
            .catch(err => console.fatal(`Failed to create or update vote button : ${err}`))
    }

    _bind_messages(reposted_message, forum_message) {
        /**
         * @param button_interaction {InteractionButton}
         */
        const callback = async button_interaction => {
            const message = button_interaction.message()

            const big_key = this.bound_message[make_key(message)]

            const vote_group = this.module_config.repost_votes[big_key]
            const author = button_interaction.author().id()
            if (vote_group) {
                // Update data
                if (button_interaction.button_id() === 'yes') {
                    if (vote_group.vote_yes.indexOf(author) !== -1) {
                        await button_interaction.reply(new Message().set_text('Tu a déjà voté !').set_client_only())
                        return
                    }
                    if (vote_group.vote_no.indexOf(author) !== -1)
                        vote_group.vote_no.splice(vote_group.vote_no.indexOf(author), 1)
                    vote_group.vote_yes.push(button_interaction.author().id())
                } else if (button_interaction.button_id() === 'no') {
                    if (vote_group.vote_no.indexOf(author) !== -1) {
                        await button_interaction.reply(new Message().set_text('Tu a déjà voté !').set_client_only())
                        return
                    }
                    if (vote_group.vote_yes.indexOf(author) !== -1)
                        vote_group.vote_yes.splice(vote_group.vote_no.indexOf(author), 1)
                    vote_group.vote_no.push(button_interaction.author().id())
                } else {
                    console.fatal('unknown interaction')
                }

                // Update vote count

                const message_0 = message_from_key(big_key.split('-')[0])
                if (await message_0.exists()) {
                    ;(await message_0.get_button_by_id('yes')).set_label(`Pour ✅ (${vote_group.vote_yes.length})`)
                    ;(await message_0.get_button_by_id('no')).set_label(`Contre ❌ (${vote_group.vote_no.length})`)
                    await message_0.update(message_0)
                }

                const message_1 = message_from_key(big_key.split('-')[1])
                if (await message_1.exists()) {
                    ;(await message_1.get_button_by_id('yes')).set_label(`Pour ✅ (${vote_group.vote_yes.length})`)
                    ;(await message_1.get_button_by_id('no')).set_label(`Contre ❌ (${vote_group.vote_no.length})`)
                    await message_1.update(message_1)
                }
                this.save_config()

                await button_interaction.skip()
            }
        }

        const big_key = `${make_key(reposted_message)}-${make_key(forum_message)}`
        this.bound_message[make_key(reposted_message)] = big_key
        this.bound_message[make_key(forum_message)] = big_key

        this.bind_button(reposted_message, callback)
        this.bind_button(forum_message, callback)
    }
}

module
    .exports = {Module}