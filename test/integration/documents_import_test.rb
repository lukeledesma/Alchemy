require "test_helper"
require "rubygems/package"
require "stringio"
require "zlib"

class DocumentsImportTest < ActionDispatch::IntegrationTest
  test "imports xml from xml.tar.gz upload" do
    archive = build_xml_tar_gz("sample_export.xml")
    folder = Document.create!(is_folder: true, metadata_filename: "Folder A", storage_path: "Folder A", records: [])

    assert_difference -> { Document.files.count }, 1 do
      post documents_path, params: {
        parent_id: folder.id,
        xml_file: Rack::Test::UploadedFile.new(
          archive.path,
          "application/gzip",
          original_filename: "sample_export.xml.tar.gz"
        )
      }
    end

    assert_redirected_to root_path

    imported = Document.files.order(:id).last
    assert_match(/\Asample_export(?: \d+)?\.xml\z/, imported.metadata_filename)
    assert_equal "192.168.1.1", imported.metadata_ip
    assert_equal "TCP", imported.metadata_protocol
    assert_equal folder.id, imported.parent_id
    assert imported.records.any?
  ensure
    archive&.close!
  end

  test "rejects import without destination folder" do
    archive = build_xml_tar_gz("sample_export.xml")

    assert_no_difference -> { Document.files.count } do
      assert_no_difference -> { Document.folders.where(metadata_filename: "Imported").count } do
        post documents_path, params: {
          xml_file: Rack::Test::UploadedFile.new(
            archive.path,
            "application/gzip",
            original_filename: "sample_export.xml.tar.gz"
          )
        }
      end
    end

    assert_redirected_to root_path
  ensure
    archive&.close!
  end

  private

  def build_xml_tar_gz(fixture_name)
    xml_content = File.binread(Rails.root.join("test/fixtures", fixture_name))

    tar_data = StringIO.new("".b)
    Gem::Package::TarWriter.new(tar_data) do |tar|
      tar.add_file_simple("inside.xml", 0o644, xml_content.bytesize) do |entry|
        entry.write(xml_content)
      end
    end

    tar_data.rewind
    tempfile = Tempfile.new(["alchemy_import", ".xml.tar.gz"])
    tempfile.binmode
    gz_data = StringIO.new("".b)
    Zlib::GzipWriter.wrap(gz_data) { |gzip| gzip.write(tar_data.string) }
    tempfile.write(gz_data.string)
    tempfile.rewind
    tempfile
  end
end
